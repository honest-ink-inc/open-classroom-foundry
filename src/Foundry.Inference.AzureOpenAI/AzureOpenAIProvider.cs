// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Foundry.Inference.AzureOpenAI;

/// <summary>
/// The district Azure OpenAI adapter. Council finding R2-12 is structural: an
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
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _deploymentId;
    private readonly string _apiVersion;
    private readonly Func<CancellationToken, Task<string>> _bearerTokenFactory;
    private readonly IOutputSchemaRegistry? _schemaRegistry;
    private readonly ProviderCapabilities _capabilities;

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
            schemaRegistry)
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
        IOutputSchemaRegistry? schemaRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(allowedEndpoints);
        ArgumentNullException.ThrowIfNull(bearerTokenFactory);

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
        _http = new HttpClient(transport, disposeHandler: true);
        _endpoint = endpoint;
        _deploymentId = deploymentId;
        _apiVersion = apiVersion;
        _bearerTokenFactory = bearerTokenFactory;
        _schemaRegistry = schemaRegistry;
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
            new Uri(_endpoint, $"openai/deployments/{_deploymentId}/chat/completions?api-version={_apiVersion}"));

        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _bearerTokenFactory(cancellationToken).ConfigureAwait(false));
        message.Content = new StringContent(BuildBody(request.Request), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return InferenceResult.Failure(InferenceOutcome.Timeout);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Map(response.StatusCode, body);
        }
    }

    private static InferenceResult Map(HttpStatusCode status, string body)
    {
        if ((int)status is >= 300 and < 400)
        {
            // Gate A confirms one exact origin. Even a same-origin redirect is
            // a different dispatch request and must be previewed afresh rather
            // than replaying the approved POST automatically.
            return InferenceResult.Failure(InferenceOutcome.PolicyRefused);
        }

        switch (status)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                return InferenceResult.Failure(InferenceOutcome.Unauthorized);
            case HttpStatusCode.TooManyRequests:
                return InferenceResult.Failure(InferenceOutcome.RateLimited);
            case >= HttpStatusCode.InternalServerError:
                return InferenceResult.Failure(InferenceOutcome.ProviderError);
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (status == HttpStatusCode.BadRequest)
            {
                var code = document.RootElement.TryGetProperty("error", out var error)
                    && error.TryGetProperty("code", out var codeElement)
                        ? codeElement.GetString()
                        : null;
                return InferenceResult.Failure(
                    code is not null && code.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
                        ? InferenceOutcome.ContentFiltered
                        : InferenceOutcome.ProviderError);
            }

            var choice = document.RootElement.GetProperty("choices")[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null;

            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceResult.Failure(InferenceOutcome.Truncated);
            }

            if (string.Equals(finishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceResult.Failure(InferenceOutcome.ContentFiltered);
            }

            var content = choice.GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content)
                ? InferenceResult.Failure(InferenceOutcome.MalformedOutput)
                : InferenceResult.Success(content);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return InferenceResult.Failure(InferenceOutcome.MalformedOutput);
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
