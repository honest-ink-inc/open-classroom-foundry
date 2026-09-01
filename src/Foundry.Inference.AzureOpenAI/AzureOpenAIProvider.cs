// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Foundry.Inference.AzureOpenAI;

/// <summary>
/// The district Azure OpenAI adapter. Product-owner-adopted rehearsal requirement
/// R2-12 is structural: an
/// endpoint not on the district allowlist makes this provider unconstructable —
/// there is no setter, no late bind, no way to point it elsewhere. Auth is a
/// caller-supplied bearer-token factory (Entra); no API key exists anywhere in
/// this codebase. Requests are stateless chat completions with JSON output and
/// temperature zero; every failure maps to the plan §12 taxonomy rather than
/// leaking as an exception. Strict per-recipe JSON schemas bind here when the
/// schema registry lands; until then json_object mode plus the engine's strict
/// parsers carry the contract.
/// </summary>
public sealed class AzureOpenAIProvider : IInferenceProvider, IDisposable
{
    internal const int ProductionMaxResponseBytes = 1024 * 1024;
    internal const int MaxResponseJsonDepth = 16;
    internal const int MaxDeploymentIdentifierLength = 128;
    internal const int MaxApiVersionLength = 64;

    private static readonly TimeSpan ProductionTotalDeadline = TimeSpan.FromSeconds(100);
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly JsonDocumentOptions StrictJsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaxResponseJsonDepth,
    };

    private readonly HttpClient _http;
    private readonly Uri _requestUri;
    private readonly Func<CancellationToken, Task<string>> _bearerTokenFactory;
    private readonly IOutputSchemaRegistry? _schemaRegistry;
    private readonly ProviderCapabilities _capabilities;
    private readonly int _maxResponseBytes;
    private readonly TimeSpan _totalDeadline;
    private readonly Action? _responseValidationStarting;

    public AzureOpenAIProvider(
        Uri endpoint,
        string deploymentId,
        IReadOnlyCollection<string> allowedEndpoints,
        Func<CancellationToken, Task<string>> bearerTokenFactory,
        string apiVersion = "2024-10-21",
        IOutputSchemaRegistry? schemaRegistry = null)
        : this(
            new SocketsHttpHandler { AllowAutoRedirect = false },
            endpoint,
            deploymentId,
            allowedEndpoints,
            bearerTokenFactory,
            apiVersion,
            schemaRegistry,
            ProductionMaxResponseBytes,
            ProductionTotalDeadline)
    {
    }

    /// <summary>
    /// Contract-test seam. Production always owns a no-redirect
    /// <see cref="SocketsHttpHandler"/> created by the public constructor.
    /// </summary>
    internal AzureOpenAIProvider(
        HttpMessageHandler transport,
        Uri endpoint,
        string deploymentId,
        IReadOnlyCollection<string> allowedEndpoints,
        Func<CancellationToken, Task<string>> bearerTokenFactory,
        string apiVersion = "2024-10-21",
        IOutputSchemaRegistry? schemaRegistry = null,
        int maxResponseBytes = ProductionMaxResponseBytes,
        TimeSpan? totalDeadline = null,
        Action? responseValidationStarting = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(allowedEndpoints);
        ArgumentNullException.ThrowIfNull(bearerTokenFactory);

        ValidateDeploymentIdentifier(deploymentId);
        ValidateApiVersion(apiVersion);

        if (maxResponseBytes is <= 0 or > ProductionMaxResponseBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResponseBytes),
                $"The response-byte limit must be between 1 and {ProductionMaxResponseBytes}.");
        }

        var effectiveDeadline = totalDeadline ?? ProductionTotalDeadline;
        if (effectiveDeadline <= TimeSpan.Zero || effectiveDeadline > ProductionTotalDeadline)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDeadline),
                $"The total provider deadline must be positive and no longer than {ProductionTotalDeadline.TotalSeconds} seconds.");
        }

        var origin = InferenceEndpointOrigin.Normalize(endpoint);
        var allowed = allowedEndpoints
            .Select(e => InferenceEndpointOrigin.TryNormalize(e, out var normalized) ? normalized : null)
            .Any(o => string.Equals(o, origin, StringComparison.Ordinal));

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"'{origin}' is not on the district endpoint allowlist; this provider cannot be constructed (R2-12).");
        }

        DisableAutomaticRedirects(transport);
        _http = new HttpClient(transport, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _requestUri = BuildRequestUri(origin, deploymentId, apiVersion);
        _bearerTokenFactory = bearerTokenFactory;
        _schemaRegistry = schemaRegistry;
        _maxResponseBytes = maxResponseBytes;
        _totalDeadline = effectiveDeadline;
        _responseValidationStarting = responseValidationStarting;
        _capabilities = new ProviderCapabilities(
            "azure-openai", deploymentId, PinnedModelVersion: null,
            SupportsImageInput: true, SupportsStructuredOutput: true,
            EndpointOrigin: origin);
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_capabilities);
    }

    public void Dispose() => _http.Dispose();

    public async Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EgressGate.EnsureProviderMatches(request, _capabilities);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            _requestUri);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_totalDeadline);
        var operationToken = deadline.Token;

        try
        {
            string bearerToken;
            try
            {
                bearerToken = await _bearerTokenFactory(operationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Authentication is a caller-supplied external boundary. Its
                // recoverable failures must remain in the provider taxonomy.
                operationToken.ThrowIfCancellationRequested();
                return InferenceResult.Failure(InferenceOutcome.ProviderError);
            }

            operationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                operationToken.ThrowIfCancellationRequested();
                return InferenceResult.Failure(InferenceOutcome.Unauthorized);
            }

            try
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }
            catch (FormatException)
            {
                operationToken.ThrowIfCancellationRequested();
                return InferenceResult.Failure(InferenceOutcome.Unauthorized);
            }

            message.Content = new StringContent(BuildBody(request.Request), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                operationToken).ConfigureAwait(false);
            operationToken.ThrowIfCancellationRequested();

            if (TryMapBodyIndependentStatus(response.StatusCode, out var statusResult))
            {
                operationToken.ThrowIfCancellationRequested();
                return statusResult;
            }

            var successResponse = response.StatusCode == HttpStatusCode.OK;
            if (!HasSupportedJsonContentType(response.Content.Headers.ContentType))
            {
                operationToken.ThrowIfCancellationRequested();
                return InferenceResult.Failure(
                    successResponse ? InferenceOutcome.MalformedOutput : InferenceOutcome.ProviderError);
            }

            if (response.Content.Headers.ContentLength is > 0
                && response.Content.Headers.ContentLength > _maxResponseBytes)
            {
                operationToken.ThrowIfCancellationRequested();
                return InferenceResult.Failure(
                    successResponse ? InferenceOutcome.MalformedOutput : InferenceOutcome.ProviderError);
            }

            var responseBuffer = ArrayPool<byte>.Shared.Rent(_maxResponseBytes + 1);
            try
            {
                var responseLength = await ReadBoundedResponseBodyAsync(
                    response.Content,
                    responseBuffer.AsMemory(0, _maxResponseBytes + 1),
                    operationToken).ConfigureAwait(false);

                if (responseLength > _maxResponseBytes)
                {
                    operationToken.ThrowIfCancellationRequested();
                    return InferenceResult.Failure(
                        successResponse ? InferenceOutcome.MalformedOutput : InferenceOutcome.ProviderError);
                }

                operationToken.ThrowIfCancellationRequested();
                var responseBytes = responseBuffer.AsMemory(0, responseLength);
                _responseValidationStarting?.Invoke();
                var result = successResponse
                    ? MapSuccessfulResponse(responseBytes)
                    : MapBadRequest(responseBytes);
                operationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(responseBuffer.AsSpan());
                ArrayPool<byte>.Shared.Return(responseBuffer, clearArray: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return InferenceResult.Failure(InferenceOutcome.Timeout);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return InferenceResult.Failure(InferenceOutcome.ProviderError);
        }
    }

    private static bool TryMapBodyIndependentStatus(HttpStatusCode status, out InferenceResult result)
    {
        if ((int)status is >= 300 and < 400)
        {
            // Gate A confirms one exact origin. Even a same-origin redirect is
            // a different dispatch request and must be previewed afresh rather
            // than replaying the approved POST automatically.
            result = InferenceResult.Failure(InferenceOutcome.PolicyRefused);
            return true;
        }

        result = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => InferenceResult.Failure(InferenceOutcome.Unauthorized),
            HttpStatusCode.RequestTimeout
                => InferenceResult.Failure(InferenceOutcome.Timeout),
            HttpStatusCode.TooManyRequests
                => InferenceResult.Failure(InferenceOutcome.RateLimited),
            >= HttpStatusCode.InternalServerError
                => InferenceResult.Failure(InferenceOutcome.ProviderError),
            _ => InferenceResult.Failure(InferenceOutcome.ProviderError),
        };

        return status is not HttpStatusCode.OK and not HttpStatusCode.BadRequest;
    }

    private static InferenceResult MapBadRequest(ReadOnlyMemory<byte> body)
    {
        if (!IsStrictUtf8(body.Span))
        {
            return InferenceResult.Failure(InferenceOutcome.ProviderError);
        }

        try
        {
            using var document = JsonDocument.Parse(body, StrictJsonOptions);
            if (HasDuplicatePropertyNames(document.RootElement)
                || document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("code", out var codeElement)
                || codeElement.ValueKind != JsonValueKind.String)
            {
                return InferenceResult.Failure(InferenceOutcome.ProviderError);
            }

            return string.Equals(codeElement.GetString(), "content_filter", StringComparison.OrdinalIgnoreCase)
                ? InferenceResult.Failure(InferenceOutcome.ContentFiltered)
                : InferenceResult.Failure(InferenceOutcome.ProviderError);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return InferenceResult.Failure(InferenceOutcome.ProviderError);
        }
    }

    private static InferenceResult MapSuccessfulResponse(ReadOnlyMemory<byte> body)
    {
        if (!IsStrictUtf8(body.Span))
        {
            return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
        }

        try
        {
            using var document = JsonDocument.Parse(body, StrictJsonOptions);
            if (HasDuplicatePropertyNames(document.RootElement)
                || document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() != 1)
            {
                return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
            }

            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object
                || !choice.TryGetProperty("finish_reason", out var finishReasonElement)
                || finishReasonElement.ValueKind != JsonValueKind.String)
            {
                return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
            }

            var finishReason = finishReasonElement.GetString();
            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceResult.Failure(InferenceOutcome.Truncated);
            }

            if (string.Equals(finishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceResult.Failure(InferenceOutcome.ContentFiltered);
            }

            if (!string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase)
                || !choice.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("role", out var role)
                || role.ValueKind != JsonValueKind.String
                || !string.Equals(role.GetString(), "assistant", StringComparison.Ordinal)
                || HasNonNullProperty(message, "tool_calls")
                || HasNonNullProperty(message, "function_call"))
            {
                return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
            }

            if (!TryReadNullableString(message, "content", out var content, out var contentMalformed)
                || contentMalformed
                || !TryReadNullableString(message, "refusal", out var refusal, out var refusalMalformed)
                || refusalMalformed)
            {
                return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
            }

            if (refusal is not null)
            {
                return string.IsNullOrWhiteSpace(refusal) || !string.IsNullOrWhiteSpace(content)
                    ? InferenceResult.Failure(InferenceOutcome.MalformedOutput)
                    : InferenceResult.Failure(InferenceOutcome.Refusal);
            }

            if (string.IsNullOrWhiteSpace(content) || !IsStrictStructuredObject(content))
            {
                return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
            }

            return InferenceResult.Success(content);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
        }
    }

    private static bool HasNonNullProperty(JsonElement parent, string propertyName)
        => parent.TryGetProperty(propertyName, out var property)
            && property.ValueKind != JsonValueKind.Null;

    private static bool TryReadNullableString(
        JsonElement parent,
        string propertyName,
        out string? value,
        out bool malformed)
    {
        value = null;
        malformed = false;
        if (!parent.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            malformed = true;
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool IsStrictStructuredObject(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content, StrictJsonOptions);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && !HasDuplicatePropertyNames(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsStrictUtf8(ReadOnlySpan<byte> content)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool HasDuplicatePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicatePropertyNames(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (HasDuplicatePropertyNames(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasSupportedJsonContentType(MediaTypeHeaderValue? contentType)
    {
        if (contentType?.MediaType is null
            || !string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (contentType.CharSet is null)
        {
            return true;
        }

        var charset = contentType.CharSet.Trim().Trim('"');
        return string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ReadBoundedResponseBodyAsync(
        HttpContent content,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var total = 0;
        while (total < destination.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static Uri BuildRequestUri(string origin, string deploymentId, string apiVersion)
        => new(
            $"{origin}/openai/deployments/{Uri.EscapeDataString(deploymentId)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}",
            UriKind.Absolute);

    private static void ValidateDeploymentIdentifier(string deploymentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (deploymentId.Length > MaxDeploymentIdentifierLength
            || deploymentId is "." or ".."
            || deploymentId.Any(character => !IsUnreservedAscii(character)))
        {
            throw new ArgumentException(
                $"A deployment identifier must be one unreserved URI segment no longer than {MaxDeploymentIdentifierLength} characters.",
                nameof(deploymentId));
        }
    }

    private static bool IsUnreservedAscii(char character)
        => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-' or '.' or '_' or '~';

    private static void ValidateApiVersion(string apiVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);
        if (apiVersion.Length > MaxApiVersionLength
            || apiVersion.Any(character => char.IsControl(character) || char.IsSurrogate(character)))
        {
            throw new ArgumentException(
                $"An API version must be visible query data no longer than {MaxApiVersionLength} characters.",
                nameof(apiVersion));
        }
    }

    private static void DisableAutomaticRedirects(HttpMessageHandler transport)
    {
        switch (transport)
        {
            case SocketsHttpHandler sockets:
                sockets.AllowAutoRedirect = false;
                break;
            case HttpClientHandler client:
                client.AllowAutoRedirect = false;
                break;
        }
    }

    private string BuildBody(InferenceRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("temperature", 0);
            writer.WriteNumber("n", 1);

            // Strict schema binding when the registry knows this schema: malformed
            // output becomes unrepresentable at generation time. Otherwise JSON-object
            // mode — the engine's strict parsers still hold the line.
            var schemaJson = _schemaRegistry?.FindSchemaJson(request.OutputSchemaId);
            writer.WriteStartObject("response_format");
            if (schemaJson is not null)
            {
                writer.WriteString("type", "json_schema");
                writer.WriteStartObject("json_schema");
                writer.WriteString("name", request.OutputSchemaId.Replace('.', '_'));
                writer.WriteBoolean("strict", true);
                writer.WritePropertyName("schema");
                using (var schema = JsonDocument.Parse(schemaJson))
                {
                    schema.RootElement.WriteTo(writer);
                }

                writer.WriteEndObject();
            }
            else
            {
                writer.WriteString("type", "json_object");
            }

            writer.WriteEndObject();

            writer.WriteStartArray("messages");
            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteStartArray("content");

            foreach (var part in request.Parts)
            {
                writer.WriteStartObject();
                switch (part)
                {
                    case TextPart text:
                        writer.WriteString("type", "text");
                        writer.WriteString("text", text.Text);
                        break;
                    case ImagePart image:
                        writer.WriteString("type", "image_url");
                        writer.WriteStartObject("image_url");
                        writer.WriteString("url", $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Bytes.Span)}");
                        writer.WriteEndObject();
                        break;
                    default:
                        throw new NotSupportedException($"Unknown inference part {part.GetType().Name}.");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
