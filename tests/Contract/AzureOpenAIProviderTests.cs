using System.Net;
using System.Text;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.AzureOpenAI;
using Foundry.Inference.Synthetic;
using Xunit;

namespace Foundry.Tests.Contract;

public class AzureOpenAIProviderTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private const string Endpoint = "https://district.example/";
    private static readonly string[] Allowlist = ["https://district.example"];

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static AzureOpenAIProvider Provider(StubHandler handler) => new(
        new HttpClient(handler),
        new Uri(Endpoint),
        "district-gpt",
        Allowlist,
        _ => Task.FromResult("entra-token"));

    private static PreviewedRequest Previewed()
    {
        var request = new InferenceRequest(
            "all-aboard.task-strip", "0.1.0", "schema.all-aboard.v1",
            [new TextPart("Task: watering."), new ImagePart(new byte[] { 1, 2 }, "image/png")],
            DataLane.Green);
        return EgressGate.Confirm(
            EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities),
            "teacher@example.org", SomeInstant);
    }

    [Fact]
    public void An_endpoint_off_the_allowlist_makes_the_provider_unconstructable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new AzureOpenAIProvider(
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
            new Uri("https://elsewhere.example/"),
            "district-gpt",
            Allowlist,
            _ => Task.FromResult("token")));

        Assert.Contains("allowlist", exception.Message, StringComparison.Ordinal);
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
        Assert.True(capabilities.SupportsStructuredOutput);
    }
}
