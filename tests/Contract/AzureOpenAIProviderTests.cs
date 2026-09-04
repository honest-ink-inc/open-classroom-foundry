using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.AzureOpenAI;

namespace Foundry.Tests.Contract;

public class AzureOpenAIProviderTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private const string Endpoint = "https://district.example/";
    private const string EmptyObjectSchema =
        """{"type":"object","properties":{},"required":[],"additionalProperties":false}""";
    private static readonly string[] Allowlist = ["https://district.example"];
    private static readonly ProviderCapabilities AzureCapabilities = new(
        "azure-openai",
        "district-gpt",
        PinnedModelVersion: null,
        SupportsImageInput: true,
        SupportsStructuredOutput: true,
        EndpointOrigin: "https://district.example");

    private sealed class FixedPolicy(DistrictPolicy current) : IDistrictPolicyProvider
    {
        public DistrictPolicy Current { get; } = current;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHandler(HttpStatusCode status, string body)
            : this((_, _) => Task.FromResult(JsonResponse(status, body)))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        public int RequestCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respond(request, cancellationToken);
        }
    }

    private sealed class CountingStream(
        byte[] bytes,
        bool blockAtEnd = false,
        Action? afterRead = null) : Stream
    {
        private int _position;

        public int BytesRead { get; private set; }

        public int ReadAttempts { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadAttempts++;
            var available = bytes.Length - _position;
            if (available <= 0)
            {
                return 0;
            }

            var copied = Math.Min(count, available);
            bytes.AsSpan(_position, copied).CopyTo(buffer.AsSpan(offset, copied));
            _position += copied;
            BytesRead += copied;
            afterRead?.Invoke();
            return copied;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadAttempts++;
            cancellationToken.ThrowIfCancellationRequested();
            var available = bytes.Length - _position;
            if (available <= 0)
            {
                if (blockAtEnd)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return 0;
            }

            var copied = Math.Min(buffer.Length, available);
            bytes.AsMemory(_position, copied).CopyTo(buffer);
            _position += copied;
            BytesRead += copied;
            afterRead?.Invoke();
            return copied;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream(Exception exception) : Stream
    {
        public int ReadAttempts { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadAttempts++;
            throw exception;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadAttempts++;
            return ValueTask.FromException<int>(exception);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static AzureOpenAIProvider Provider(
        HttpMessageHandler handler,
        Func<CancellationToken, Task<string>>? bearerTokenFactory = null,
        Uri? endpoint = null,
        string deploymentId = "district-gpt",
        string apiVersion = "2024-10-21",
        int maxResponseBytes = AzureOpenAIProvider.ProductionMaxResponseBytes,
        TimeSpan? totalDeadline = null,
        Action? responseValidationStarting = null,
        IOutputSchemaRegistry? schemaRegistry = null) => new(
        handler,
        endpoint ?? new Uri(Endpoint),
        deploymentId,
        Allowlist,
        bearerTokenFactory ?? (_ => Task.FromResult("entra-token")),
        apiVersion,
        schemaRegistry: schemaRegistry ?? TestSchemas(),
        maxResponseBytes,
        totalDeadline,
        responseValidationStarting);

    private static InMemorySchemaRegistry TestSchemas()
        => new(new Dictionary<string, string>
        {
            ["schema.all-aboard.v1"] = EmptyObjectSchema,
        });

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
        => Response(
            status,
            new MemoryStream(Encoding.UTF8.GetBytes(body), writable: false),
            mediaType: "application/json",
            charset: "utf-8");

    private static HttpResponseMessage Response(
        HttpStatusCode status,
        Stream stream,
        string? mediaType = "application/json",
        string? charset = "utf-8",
        long? declaredLength = null)
    {
        var content = new StreamContent(stream);
        if (mediaType is not null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType)
            {
                CharSet = charset,
            };
        }

        if (declaredLength is not null)
        {
            content.Headers.ContentLength = declaredLength;
        }

        return new HttpResponseMessage(status) { Content = content };
    }

    private static string SuccessEnvelope(string structuredContent = "{}")
        => "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":"
            + JsonSerializer.Serialize(structuredContent)
            + ",\"refusal\":null}}]}";

    private static string NestedObject(int depth)
    {
        var value = "{}";
        for (var level = 0; level < depth; level++)
        {
            value = "{\"nested\":" + value + "}";
        }

        return value;
    }

    private static PreviewedRequest Previewed(ProviderCapabilities? capabilities = null)
    {
        var request = new InferenceRequest(
            "all-aboard.task-strip", "0.1.0", "schema.all-aboard.v1",
            [new TextPart("Task: watering."), new ImagePart(new byte[] { 1, 2 }, "image/png")],
            DataLane.Green);
        return EgressGate.Confirm(
            EgressGate.Preview(request, capabilities ?? AzureCapabilities),
            "teacher@example.org", SomeInstant);
    }

    [Fact]
    public void An_endpoint_off_the_allowlist_makes_the_provider_unconstructable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new AzureOpenAIProvider(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new Uri("https://elsewhere.example/"),
            "district-gpt",
            Allowlist,
            _ => Task.FromResult("token"),
            schemaRegistry: TestSchemas()));

        Assert.Contains("allowlist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_insecure_endpoint_is_unconstructable_even_when_its_spelling_is_allowlisted()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AzureOpenAIProvider(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new Uri("http://district.example/"),
            "district-gpt",
            ["http://district.example"],
            _ => Task.FromResult("token"),
            schemaRegistry: TestSchemas()));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_transports_have_automatic_redirects_disabled_structurally()
    {
        var sockets = new SocketsHttpHandler { AllowAutoRedirect = true };

        using var provider = new AzureOpenAIProvider(
            sockets,
            new Uri(Endpoint),
            "district-gpt",
            Allowlist,
            _ => Task.FromResult("token"),
            schemaRegistry: TestSchemas());

        Assert.False(sockets.AllowAutoRedirect);
    }

    [Fact]
    public async Task A_redirect_response_is_refused_without_replaying_the_confirmed_post()
    {
        var handler = new StubHandler(HttpStatusCode.TemporaryRedirect, "{}");
        using var provider = Provider(handler);

        var result = await provider.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("https://district.example", handler.LastRequest!.RequestUri!.GetLeftPart(UriPartial.Authority));
    }

    [Fact]
    public async Task A_successful_completion_returns_structured_output_with_bearer_auth_and_zero_temperature()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{}"}}]}""");

        var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("{}", result.StructuredJson);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.DoesNotContain("api-key", handler.LastBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"temperature\":0", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"n\":1", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"json_schema\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"strict\":true", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("district-gpt/chat/completions", handler.LastRequest.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}", InferenceOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, "{}", InferenceOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", InferenceOutcome.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, "{}", InferenceOutcome.ProviderError)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":"content_filter"}}""", InferenceOutcome.ContentFiltered)]
    [InlineData(HttpStatusCode.OK, """{"choices":[{"finish_reason":"length","message":{"content":"{"}}]}""", InferenceOutcome.Truncated)]
    [InlineData(HttpStatusCode.OK, """{"choices":[{"finish_reason":"content_filter","message":{"content":""}}]}""", InferenceOutcome.ContentFiltered)]
    [InlineData(HttpStatusCode.OK, "not json", InferenceOutcome.MalformedOutput)]
    public async Task Every_failure_maps_to_the_plan_taxonomy(HttpStatusCode status, string body, InferenceOutcome expected)
    {
        var result = await Provider(new StubHandler(status, body)).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Capabilities_name_the_provider_and_deployment_for_the_evaluation_record()
    {
        var capabilities = await Provider(new StubHandler(HttpStatusCode.OK, "{}"))
            .GetCapabilitiesAsync(CancellationToken.None);

        Assert.Equal("azure-openai", capabilities.ProviderId);
        Assert.Equal("district-gpt", capabilities.DeploymentId);
        Assert.Equal("https://district.example", capabilities.EndpointOrigin);
        Assert.True(capabilities.SupportsStructuredOutput);
    }

    [Theory]
    [InlineData("synthetic", "district-gpt")]
    [InlineData("Azure-OpenAI", "district-gpt")]
    [InlineData("azure-openai", "other-deployment")]
    [InlineData("azure-openai", "District-Gpt")]
    public async Task A_confirmation_for_another_provider_or_deployment_is_rejected_before_auth_or_http(
        string providerId,
        string deploymentId)
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""");
        var tokenRequests = 0;
        var provider = Provider(handler, _ =>
        {
            tokenRequests++;
            return Task.FromResult("entra-token");
        });
        var mismatchedCapabilities = AzureCapabilities with
        {
            ProviderId = providerId,
            DeploymentId = deploymentId,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CompleteAsync(Previewed(mismatchedCapabilities), CancellationToken.None));

        Assert.Equal(0, tokenRequests);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task A_confirmation_for_another_endpoint_origin_is_rejected_before_auth_or_http()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""");
        var tokenRequests = 0;
        var provider = Provider(handler, _ =>
        {
            tokenRequests++;
            return Task.FromResult("entra-token");
        });
        var oldEndpointCapabilities = AzureCapabilities with
        {
            EndpointOrigin = "https://retired-district.example",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CompleteAsync(Previewed(oldEndpointCapabilities), CancellationToken.None));

        Assert.Equal(0, tokenRequests);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task District_policy_cannot_be_bypassed_by_the_providers_arbitrary_constructor_allowlist()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""");
        var tokenRequests = 0;
        var provider = Provider(handler, _ =>
        {
            tokenRequests++;
            return Task.FromResult("entra-token");
        });
        var districtPolicy = new DistrictPolicy(
            ["https://different-district.example/openai"],
            "azure-openai",
            "district-gpt",
            DataLane.Green,
            CloudInferenceEnabled: true);
        var gated = new PolicyGatedInferenceProvider(provider, new FixedPolicy(districtPolicy));

        var result = await gated.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(0, tokenRequests);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Body_independent_statuses_are_classified_without_opening_the_body()
    {
        (HttpStatusCode Status, InferenceOutcome Expected)[] cases =
        [
            (HttpStatusCode.TemporaryRedirect, InferenceOutcome.PolicyRefused),
            (HttpStatusCode.Unauthorized, InferenceOutcome.Unauthorized),
            (HttpStatusCode.Forbidden, InferenceOutcome.Unauthorized),
            (HttpStatusCode.RequestTimeout, InferenceOutcome.Timeout),
            (HttpStatusCode.TooManyRequests, InferenceOutcome.RateLimited),
            (HttpStatusCode.InternalServerError, InferenceOutcome.ProviderError),
            (HttpStatusCode.NotFound, InferenceOutcome.ProviderError),
            (HttpStatusCode.Created, InferenceOutcome.ProviderError),
            (HttpStatusCode.PartialContent, InferenceOutcome.ProviderError),
        ];

        foreach (var (status, expected) in cases)
        {
            var stream = new ThrowingReadStream(new InvalidOperationException("A classified response body was read."));
            var handler = new StubHandler((_, _) => Task.FromResult(Response(status, stream)));

            var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

            Assert.Equal(expected, result.Outcome);
            Assert.Equal(0, stream.ReadAttempts);
        }
    }

    [Fact]
    public async Task A_declared_oversized_success_is_rejected_without_opening_the_body()
    {
        const int cap = 128;
        var stream = new CountingStream(Encoding.UTF8.GetBytes(SuccessEnvelope()));
        var handler = new StubHandler((_, _) => Task.FromResult(
            Response(HttpStatusCode.OK, stream, declaredLength: cap + 1L)));

        var result = await Provider(handler, maxResponseBytes: cap)
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
        Assert.Equal(0, stream.ReadAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_lying_lengths_are_read_only_to_cap_plus_one(bool declareSmallLength)
    {
        const int cap = 128;
        var stream = new CountingStream(new byte[cap + 100]);
        var handler = new StubHandler((_, _) => Task.FromResult(Response(
            HttpStatusCode.OK,
            stream,
            declaredLength: declareSmallLength ? 1 : null)));

        var result = await Provider(handler, maxResponseBytes: cap)
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
        Assert.Equal(cap + 1, stream.BytesRead);
    }

    [Fact]
    public async Task A_valid_response_exactly_at_the_byte_boundary_is_accepted()
    {
        var json = Encoding.UTF8.GetBytes(SuccessEnvelope());
        var cap = json.Length + 32;
        var exact = new byte[cap];
        json.CopyTo(exact, 0);
        exact.AsSpan(json.Length).Fill((byte)' ');
        var stream = new CountingStream(exact);
        var handler = new StubHandler((_, _) => Task.FromResult(
            Response(HttpStatusCode.OK, stream, declaredLength: cap)));

        var result = await Provider(handler, maxResponseBytes: cap)
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("{}", result.StructuredJson);
        Assert.Equal(cap, stream.BytesRead);
    }

    [Fact]
    public async Task Success_requires_application_json_with_absent_or_utf8_charset()
    {
        (string? MediaType, string? Charset)[] rejected =
        [
            (null, null),
            ("text/plain", "utf-8"),
            ("application/json", "utf-16"),
            ("application/json", "iso-8859-1"),
        ];

        foreach (var (mediaType, charset) in rejected)
        {
            var stream = new CountingStream(Encoding.UTF8.GetBytes(SuccessEnvelope()));
            var handler = new StubHandler((_, _) => Task.FromResult(
                Response(HttpStatusCode.OK, stream, mediaType, charset)));

            var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

            Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
            Assert.Equal(0, stream.ReadAttempts);
        }

        foreach (var charset in new string?[] { null, "UTF-8" })
        {
            var handler = new StubHandler((_, _) => Task.FromResult(Response(
                HttpStatusCode.OK,
                new MemoryStream(Encoding.UTF8.GetBytes(SuccessEnvelope()), writable: false),
                charset: charset)));
            Assert.True((await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None)).IsSuccess);
        }
    }

    [Fact]
    public async Task Invalid_utf8_is_malformed_instead_of_replacement_decoded()
    {
        var prefix = Encoding.UTF8.GetBytes(
            "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{\\\"value\\\":\\\"");
        var suffix = Encoding.UTF8.GetBytes("\\\"}\"}}]}");
        var bytes = prefix.Concat(new byte[] { 0xFF }).Concat(suffix).ToArray();
        var handler = new StubHandler((_, _) => Task.FromResult(Response(
            HttpStatusCode.OK,
            new MemoryStream(bytes, writable: false))));

        var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
    }

    [Fact]
    public async Task Outer_and_inner_json_above_the_explicit_depth_are_malformed()
    {
        var innerTooDeep = SuccessEnvelope(NestedObject(AzureOpenAIProvider.MaxResponseJsonDepth + 2));
        var outerTooDeep = "{\"extra\":"
            + NestedObject(AzureOpenAIProvider.MaxResponseJsonDepth + 2)
            + ",\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}";

        foreach (var responseBody in new[] { innerTooDeep, outerTooDeep })
        {
            var result = await Provider(new StubHandler(HttpStatusCode.OK, responseBody))
                .CompleteAsync(Previewed(), CancellationToken.None);
            Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
        }
    }

    [Fact]
    public async Task Duplicate_envelope_and_structured_output_names_are_malformed()
    {
        (string Hint, string Body)[] cases =
        [
            ("root choices", "{\"choices\":[],\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}"),
            ("finish reason", "{\"choices\":[{\"finish_reason\":\"length\",\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}"),
            ("message", "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"},\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}"),
            ("content", "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\",\"content\":\"{}\"}}]}"),
            ("refusal", "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":null,\"refusal\":\"first\",\"refusal\":\"second\"}}]}"),
            ("inner output", SuccessEnvelope("{\"same\":1,\"same\":2}")),
        ];

        foreach (var (hint, responseBody) in cases)
        {
            var result = await Provider(new StubHandler(HttpStatusCode.OK, responseBody))
                .CompleteAsync(Previewed(), CancellationToken.None);
            Assert.True(
                result.Outcome == InferenceOutcome.MalformedOutput,
                $"The {hint} duplicate returned {result.Outcome}.");
        }
    }

    [Theory]
    [InlineData("{\"content\":\"{}\"}")]
    [InlineData("{\"role\":\"tool\",\"content\":\"{}\"}")]
    [InlineData("{\"role\":\"assistant\",\"content\":\"{}\",\"tool_calls\":[]}")]
    [InlineData("{\"role\":\"assistant\",\"content\":\"{}\",\"function_call\":{}}")]
    public async Task Success_requires_one_assistant_message_without_tool_or_function_calls(string messageJson)
    {
        var body = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":" + messageJson + "}]}";

        var result = await Provider(new StubHandler(HttpStatusCode.OK, body))
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
    }

    [Fact]
    public async Task A_documented_nonblank_message_refusal_is_typed_and_carries_no_output()
    {
        var body = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":null,\"refusal\":\"The request was refused.\"}}]}";

        var result = await Provider(new StubHandler(HttpStatusCode.OK, body))
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.Refusal, result.Outcome);
        Assert.Null(result.StructuredJson);
    }

    [Fact]
    public async Task Mixed_blank_or_wrong_typed_refusals_are_malformed()
    {
        string[] cases =
        [
            "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\",\"refusal\":\"no\"}}]}",
            "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":null,\"refusal\":\"   \"}}]}",
            "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":null,\"refusal\":true}}]}",
        ];

        foreach (var responseBody in cases)
        {
            var result = await Provider(new StubHandler(HttpStatusCode.OK, responseBody))
                .CompleteAsync(Previewed(), CancellationToken.None);
            Assert.Equal(InferenceOutcome.MalformedOutput, result.Outcome);
        }
    }

    [Fact]
    public async Task A_bad_request_maps_only_one_exact_content_filter_code()
    {
        var filtered = await Provider(new StubHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"code\":\"content_filter\"}}"))
            .CompleteAsync(Previewed(), CancellationToken.None);
        var lookalike = await Provider(new StubHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"code\":\"not_content_filter\"}}"))
            .CompleteAsync(Previewed(), CancellationToken.None);
        var duplicate = await Provider(new StubHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"code\":\"other\",\"code\":\"content_filter\"}}"))
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.ContentFiltered, filtered.Outcome);
        Assert.Equal(InferenceOutcome.ProviderError, lookalike.Outcome);
        Assert.Equal(InferenceOutcome.ProviderError, duplicate.Outcome);
    }

    [Fact]
    public async Task Transport_and_body_io_failures_are_provider_errors_not_timeouts()
    {
        var sendFailure = new StubHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic transport failure")));
        var sendResult = await Provider(sendFailure).CompleteAsync(Previewed(), CancellationToken.None);

        var bodyFailure = new ThrowingReadStream(new IOException("synthetic response interruption"));
        var bodyHandler = new StubHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, bodyFailure)));
        var bodyResult = await Provider(bodyHandler).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.ProviderError, sendResult.Outcome);
        Assert.Equal(InferenceOutcome.ProviderError, bodyResult.Outcome);
        Assert.Equal(1, bodyFailure.ReadAttempts);
    }

    [Fact]
    public async Task Caller_cancellation_during_body_read_is_rethrown()
    {
        var stream = new CountingStream([], blockAtEnd: true);
        var handler = new StubHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, stream)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var provider = Provider(handler, totalDeadline: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(Previewed(), cancellation.Token));
    }

    [Fact]
    public async Task Caller_cancellation_that_arrives_during_response_validation_cannot_return_success()
    {
        using var cancellation = new CancellationTokenSource();
        using var provider = Provider(
            new StubHandler(HttpStatusCode.OK, SuccessEnvelope()),
            responseValidationStarting: cancellation.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(Previewed(), cancellation.Token));
    }

    [Fact]
    public async Task Caller_cancellation_on_the_cap_plus_one_read_precedes_the_oversize_outcome()
    {
        const int maximumBytes = 64;
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream(
            new byte[maximumBytes + 1],
            afterRead: cancellation.Cancel);
        var handler = new StubHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, stream)));
        using var provider = Provider(handler, maxResponseBytes: maximumBytes);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(Previewed(), cancellation.Token));
        Assert.Equal(maximumBytes + 1, stream.BytesRead);
    }

    [Fact]
    public async Task Bearer_acquisition_and_invalid_token_failures_are_typed_before_http()
    {
        var acquisitionHandler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        using var acquisitionProvider = Provider(
            acquisitionHandler,
            _ => Task.FromException<string>(new InvalidOperationException("Synthetic authentication failure.")));

        var acquisitionResult = await acquisitionProvider.CompleteAsync(
            Previewed(),
            CancellationToken.None);

        Assert.Equal(InferenceOutcome.ProviderError, acquisitionResult.Outcome);
        Assert.Equal(0, acquisitionHandler.RequestCount);

        foreach (var invalidToken in new[] { "   ", "not\r\na-token" })
        {
            var invalidHandler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
            using var invalidProvider = Provider(
                invalidHandler,
                _ => Task.FromResult(invalidToken));

            var invalidResult = await invalidProvider.CompleteAsync(
                Previewed(),
                CancellationToken.None);

            Assert.Equal(InferenceOutcome.Unauthorized, invalidResult.Outcome);
            Assert.Equal(0, invalidHandler.RequestCount);
        }
    }

    [Fact]
    public async Task Caller_cancellation_during_bearer_acquisition_prevents_http_even_if_the_factory_returns()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        using var provider = Provider(
            handler,
            _ =>
            {
                cancellation.Cancel();
                return Task.FromResult("synthetic-token");
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(Previewed(), cancellation.Token));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task The_single_total_deadline_spans_auth_and_body_read()
    {
        static async Task<string> NeverFinishAuth(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "unreachable";
        }

        var authHandler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        var authResult = await Provider(
            authHandler,
            NeverFinishAuth,
            totalDeadline: TimeSpan.FromMilliseconds(100))
            .CompleteAsync(Previewed(), CancellationToken.None);

        var stream = new CountingStream([], blockAtEnd: true);
        var bodyHandler = new StubHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, stream)));
        var bodyResult = await Provider(
            bodyHandler,
            totalDeadline: TimeSpan.FromMilliseconds(100))
            .CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.Timeout, authResult.Outcome);
        Assert.Equal(0, authHandler.RequestCount);
        Assert.Equal(InferenceOutcome.Timeout, bodyResult.Outcome);
    }

    [Fact]
    public async Task A_noncaller_operation_cancellation_is_a_timeout()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new OperationCanceledException("synthetic timeout")));

        var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(InferenceOutcome.Timeout, result.Outcome);
    }

    [Fact]
    public async Task Endpoint_path_query_and_fragment_cannot_change_the_canonical_request_target()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        using var provider = Provider(
            handler,
            endpoint: new Uri("https://district.example/untrusted/base?other=true#fragment"));

        var result = await provider.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "https://district.example/openai/deployments/district-gpt/chat/completions?api-version=2024-10-21",
            handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Api_version_is_bounded_and_escaped_as_query_data()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        using var provider = Provider(handler, apiVersion: "2024-10-21&other=true");

        var result = await provider.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.EndsWith(
            "?api-version=2024-10-21%26other%3Dtrue",
            handler.LastRequest!.RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() => Provider(
            new StubHandler(HttpStatusCode.OK, SuccessEnvelope()),
            apiVersion: new string('v', AzureOpenAIProvider.MaxApiVersionLength + 1)));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData("%2e%2e/")]
    [InlineData("one/two")]
    [InlineData("one?other=true")]
    [InlineData("one#fragment")]
    [InlineData("white space")]
    public void Unsafe_deployment_identifiers_fail_before_auth_or_http(string deploymentId)
    {
        var handler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());
        var tokenRequests = 0;

        Assert.Throws<ArgumentException>(() => Provider(
            handler,
            _ =>
            {
                tokenRequests++;
                return Task.FromResult("token");
            },
            deploymentId: deploymentId));

        Assert.Equal(0, tokenRequests);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void An_overlong_deployment_identifier_fails_before_auth_or_http()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SuccessEnvelope());

        Assert.Throws<ArgumentException>(() => Provider(
            handler,
            deploymentId: new string('d', AzureOpenAIProvider.MaxDeploymentIdentifierLength + 1)));

        Assert.Equal(0, handler.RequestCount);
    }
}
