using System.Net;
using System.Text;
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

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static AzureOpenAIProvider Provider(
        StubHandler handler,
        Func<CancellationToken, Task<string>>? bearerTokenFactory = null) => new(
        handler,
        new Uri(Endpoint),
        "district-gpt",
        Allowlist,
        bearerTokenFactory ?? (_ => Task.FromResult("entra-token")));

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
            _ => Task.FromResult("token")));

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
            _ => Task.FromResult("token")));

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
            _ => Task.FromResult("token"));

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
            """{"choices":[{"finish_reason":"stop","message":{"content":"{\"steps\":3}"}}]}""");

        var result = await Provider(handler).CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("""{"steps":3}""", result.StructuredJson);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.DoesNotContain("api-key", handler.LastBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"temperature\":0", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("json_object", handler.LastBody, StringComparison.Ordinal);
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
}
