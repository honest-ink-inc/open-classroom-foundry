using System.Net;
using System.Text;
using System.Text.Json;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.AzureOpenAI;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.Tests.Contract;

public class SchemaBindingTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProviderCapabilities AzureCapabilities = new(
        "azure-openai",
        "district-gpt",
        PinnedModelVersion: null,
        SupportsImageInput: true,
        SupportsStructuredOutput: true,
        EndpointOrigin: "https://district.example");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    private static PreviewedRequest Previewed(string schemaId)
    {
        var request = new InferenceRequest("all-aboard.task-strip", "0.1.0", schemaId,
            [new TextPart("Task: watering.")], DataLane.Green);
        return EgressGate.Confirm(
            EgressGate.Preview(request, AzureCapabilities),
            "teacher@example.org", SomeInstant);
    }

    private static AzureOpenAIProvider Provider(CapturingHandler handler, IOutputSchemaRegistry? registry) => new(
        handler, new Uri("https://district.example/"), "district-gpt",
        ["https://district.example"], _ => Task.FromResult("token"), schemaRegistry: registry);

    [Fact]
    public async Task A_registered_schema_binds_strictly_at_generation_time()
    {
        var handler = new CapturingHandler();
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.all-aboard.v1"] = TaskStripSuggestionParser.SchemaJson,
        });

        await Provider(handler, registry).CompleteAsync(Previewed("schema.all-aboard.v1"), CancellationToken.None);

        Assert.Contains("\"type\":\"json_schema\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"strict\":true", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"additionalProperties\":false", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("schema_all-aboard_v1".Replace("-", "-"), handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unregistered_schema_falls_back_to_json_object_mode()
    {
        var handler = new CapturingHandler();

        await Provider(handler, null).CompleteAsync(Previewed("schema.unregistered.v1"), CancellationToken.None);

        Assert.Contains("\"type\":\"json_object\"", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("json_schema", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public void The_all_aboard_schema_parses_and_matches_the_parsers_bounds()
    {
        using var schema = JsonDocument.Parse(TaskStripSuggestionParser.SchemaJson);
        var steps = schema.RootElement.GetProperty("properties").GetProperty("steps");

        Assert.Equal(AllAboardBuilders.MinimumSteps, steps.GetProperty("minItems").GetInt32());
        Assert.Equal(AllAboardBuilders.MaximumSteps, steps.GetProperty("maxItems").GetInt32());
        Assert.Equal(TaskStripSuggestionParser.MaximumStepLength,
            steps.GetProperty("items").GetProperty("maxLength").GetInt32());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }
}
