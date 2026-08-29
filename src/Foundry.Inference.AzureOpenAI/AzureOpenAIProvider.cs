// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Foundry.Domain;

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
public sealed class AzureOpenAIProvider : IInferenceProvider
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _deploymentId;
    private readonly string _apiVersion;
    private readonly Func<CancellationToken, Task<string>> _bearerTokenFactory;
    private readonly IOutputSchemaRegistry? _schemaRegistry;

    public AzureOpenAIProvider(
        HttpClient http,
        Uri endpoint,
        string deploymentId,
        IReadOnlyCollection<string> allowedEndpoints,
        Func<CancellationToken, Task<string>> bearerTokenFactory,
        string apiVersion = "2024-10-21",
        IOutputSchemaRegistry? schemaRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(allowedEndpoints);
        ArgumentNullException.ThrowIfNull(bearerTokenFactory);

        var origin = Origin(endpoint);
        var allowed = allowedEndpoints
            .Select(e => Uri.TryCreate(e, UriKind.Absolute, out var uri) ? Origin(uri) : null)
            .Any(o => o is not null && string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"'{origin}' is not on the district endpoint allowlist; this provider cannot be constructed (R2-12).");
        }

        _http = http;
        _endpoint = endpoint;
        _deploymentId = deploymentId;
        _apiVersion = apiVersion;
        _bearerTokenFactory = bearerTokenFactory;
        _schemaRegistry = schemaRegistry;
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderCapabilities(
            "azure-openai", _deploymentId, PinnedModelVersion: null,
            SupportsImageInput: true, SupportsStructuredOutput: true));
    }

    public async Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    private static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);
}
