using System.Globalization;
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
        private readonly string _responseBody;

        public CapturingHandler(string? structuredJson = null)
        {
            var content = structuredJson
                ?? """{"title":"Watering","steps":["One","Two","Three"]}""";
            _responseBody = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":"
                + JsonSerializer.Serialize(content)
                + ",\"refusal\":null}}]}";
        }

        public string? LastBody { get; private set; }

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
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

    private static AzureOpenAIProvider Provider(
        CapturingHandler handler,
        IOutputSchemaRegistry? registry,
        Func<CancellationToken, Task<string>>? bearerTokenFactory = null) => new(
        handler, new Uri("https://district.example/"), "district-gpt",
        ["https://district.example"], bearerTokenFactory ?? (_ => Task.FromResult("token")), schemaRegistry: registry);

    [Fact]
    public async Task A_registered_schema_binds_strictly_at_generation_time()
    {
        var handler = new CapturingHandler();
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.all-aboard.v1"] = TaskStripSuggestionParser.SchemaJson,
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.all-aboard.v1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("\"type\":\"json_schema\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"strict\":true", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"additionalProperties\":false", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("schema_all-aboard_v1".Replace("-", "-"), handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("minLength", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("maxLength", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("minItems", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("maxItems", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unregistered_schema_is_refused_before_auth_or_http()
    {
        var handler = new CapturingHandler();
        var tokenRequests = 0;
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>());
        using var provider = new AzureOpenAIProvider(
            handler,
            new Uri("https://district.example/"),
            "district-gpt",
            ["https://district.example"],
            _ =>
            {
                tokenRequests++;
                return Task.FromResult("token");
            },
            schemaRegistry: registry);

        var result = await provider.CompleteAsync(
            Previewed("schema.unregistered.v1"),
            CancellationToken.None);

        Assert.Equal(InferenceOutcome.UnsupportedCapability, result.Outcome);
        Assert.Equal(0, tokenRequests);
        Assert.Equal(0, handler.RequestCount);
        Assert.Null(handler.LastBody);
    }

    [Fact]
    public async Task A_malformed_or_unsupported_registered_schema_is_refused_before_egress()
    {
        foreach (var schemaJson in new[]
        {
            "{not-json",
            """{"type":"object","properties":{},"required":[],"additionalProperties":true}""",
            """{"type":"object","properties":{},"required":[],"additionalProperties":false,"oneOf":[]}""",
            """{"type":"object","properties":{"value":{"type":"string","pattern":".*"}},"required":["value"],"additionalProperties":false}""",
            """{"type":"object","description":42,"properties":{},"required":[],"additionalProperties":false}""",
            """{"type":"object","properties":{"value":{"type":"string","description":{}}},"required":["value"],"additionalProperties":false}""",
        })
        {
            var handler = new CapturingHandler();
            var tokenRequests = 0;
            var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
            {
                ["schema.malformed.v1"] = schemaJson,
            });

            var result = await Provider(handler, registry, _ =>
                {
                    tokenRequests++;
                    return Task.FromResult("token");
                })
                .CompleteAsync(Previewed("schema.malformed.v1"), CancellationToken.None);

            Assert.Equal(InferenceOutcome.UnsupportedCapability, result.Outcome);
            Assert.Equal(0, tokenRequests);
            Assert.Equal(0, handler.RequestCount);
        }
    }

    [Fact]
    public async Task Invalid_response_format_schema_names_are_refused_before_auth_or_http()
    {
        string[] invalidSchemaIds =
        [
            string.Empty,
            " ",
            "schema/escape",
            "schema with space",
            "schéma.unicode",
            new string('a', AzureOpenAIProvider.MaxResponseFormatSchemaNameLength + 1),
        ];

        foreach (var schemaId in invalidSchemaIds)
        {
            var handler = new CapturingHandler();
            var tokenRequests = 0;
            var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
            {
                [schemaId] = TaskStripSuggestionParser.SchemaJson,
            });
            using var provider = Provider(handler, registry, _ =>
            {
                tokenRequests++;
                return Task.FromResult("token");
            });

            var result = await provider.CompleteAsync(Previewed(schemaId), CancellationToken.None);

            Assert.Equal(InferenceOutcome.UnsupportedCapability, result.Outcome);
            Assert.Equal(0, tokenRequests);
            Assert.Equal(0, handler.RequestCount);
        }
    }

    [Fact]
    public async Task Azure_schema_depth_and_total_property_limits_fail_closed()
    {
        var nested = "{\"type\":\"string\"}";
        for (var level = 0; level < StrictOutputSchema.MaximumProviderNestingLevels; level++)
        {
            nested = "{\"type\":\"object\",\"properties\":{\"nested\":"
                + nested
                + "},\"required\":[\"nested\"],\"additionalProperties\":false}";
        }

        var propertyNames = Enumerable.Range(0, StrictOutputSchema.MaximumProviderObjectProperties + 1)
            .Select(index => $"p{index}")
            .ToArray();
        var tooManyProperties = "{\"type\":\"object\",\"properties\":{"
            + string.Join(',', propertyNames.Select(name => $"\"{name}\":{{\"type\":\"string\"}}"))
            + "},\"required\":["
            + string.Join(',', propertyNames.Select(name => $"\"{name}\""))
            + "],\"additionalProperties\":false}";

        foreach (var schemaJson in new[] { nested, tooManyProperties })
        {
            var handler = new CapturingHandler();
            var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
            {
                ["schema.limit.v1"] = schemaJson,
            });

            var result = await Provider(handler, registry)
                .CompleteAsync(Previewed("schema.limit.v1"), CancellationToken.None);

            Assert.Equal(InferenceOutcome.UnsupportedCapability, result.Outcome);
            Assert.Equal(0, handler.RequestCount);
        }
    }

    [Fact]
    public async Task Azure_total_enum_value_budget_fails_closed_before_auth_or_http()
    {
        var tooManyEnumValues = BuildSinglePropertySchema(
            "choice",
            BuildStringEnum(StrictOutputSchema.MaximumProviderEnumValues + 1, valueLength: 4));

        await AssertSchemaRefusedBeforeAuthOrHttp(tooManyEnumValues);
    }

    [Fact]
    public async Task Azure_aggregate_schema_string_budget_fails_closed_before_auth_or_http()
    {
        var longPropertyNames = Enumerable.Range(0, StrictOutputSchema.MaximumProviderObjectProperties)
            .Select(index => $"p{index:D2}_{new string('x', 148)}")
            .ToArray();
        var tooManyAggregateCharacters = BuildObjectSchema(
            longPropertyNames.Select(name => (name, "{\"type\":\"string\"}")));

        await AssertSchemaRefusedBeforeAuthOrHttp(tooManyAggregateCharacters);
    }

    [Fact]
    public async Task Azure_large_string_enum_budget_fails_closed_before_auth_or_http()
    {
        var tooLongLargeEnum = BuildSinglePropertySchema(
            "choice",
            BuildStringEnum(
                StrictOutputSchema.LargeStringEnumValueThreshold + 1,
                valueLength: 31));

        await AssertSchemaRefusedBeforeAuthOrHttp(tooLongLargeEnum);
    }

    [Theory]
    [InlineData("{\"type\":\"string\",\"enum\":[\"same\",\"\\u0073ame\"]}")]
    [InlineData("{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"enum\":[[\"same\",\"value\"],[\"\\u0073ame\",\"value\"]]}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"label\":{\"type\":\"string\"},\"values\":{\"type\":\"array\",\"items\":{\"type\":\"number\"}}},\"required\":[\"label\",\"values\"],\"additionalProperties\":false,\"enum\":[{\"label\":\"same\",\"values\":[1]},{\"values\":[1.0],\"label\":\"\\u0073ame\"}]}")]
    public async Task Azure_semantically_duplicate_enum_values_fail_before_auth_or_http(
        string propertySchema)
    {
        await AssertSchemaRefusedBeforeAuthOrHttp(
            BuildSinglePropertySchema("choice", propertySchema));
    }

    [Fact]
    public async Task Azure_malformed_unicode_in_schema_strings_fails_before_auth_or_http()
    {
        var loneHighSurrogate = new string((char)0xD800, 1);
        string[] malformedSchemas =
        [
            BuildSinglePropertySchema(
                "value",
                "{\"type\":\"string\",\"description\":\"\\uD800\"}"),
            "{\"type\":\"object\",\"properties\":{\"\\uD800\":{\"type\":\"string\"}},\"required\":[\"\\uD800\"],\"additionalProperties\":false}",
            "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"required\":[\"\\uD800\"],\"additionalProperties\":false}",
            BuildSinglePropertySchema(
                "value",
                "{\"type\":\"string\",\"enum\":[\"\\uD800\"]}"),
            BuildSinglePropertySchema(
                "value",
                "{\"type\":\"string\",\"description\":\"" + loneHighSurrogate + "\"}"),
        ];

        foreach (var schemaJson in malformedSchemas)
        {
            await AssertSchemaRefusedBeforeAuthOrHttp(schemaJson);
        }
    }

    [Fact]
    public async Task Returned_json_must_match_the_exact_registered_schema()
    {
        var handler = new CapturingHandler("""{"title":"","steps":["One","Two"]}""");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.all-aboard.v1"] = TaskStripSuggestionParser.SchemaJson,
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.all-aboard.v1"), CancellationToken.None);

        Assert.Equal(InferenceOutcome.SchemaMismatch, result.Outcome);
        Assert.Null(result.StructuredJson);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1e0")]
    public async Task Json_numbers_with_zero_fraction_match_an_integer_schema(string number)
    {
        var handler = new CapturingHandler($"{{\"value\":{number}}}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.integer.v1"] = BuildSinglePropertySchema("value", "{\"type\":\"integer\"}"),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.integer.v1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Json_number_with_a_fraction_does_not_match_an_integer_schema()
    {
        var handler = new CapturingHandler("{\"value\":1.5}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.integer.v1"] = BuildSinglePropertySchema("value", "{\"type\":\"integer\"}"),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.integer.v1"), CancellationToken.None);

        Assert.Equal(InferenceOutcome.SchemaMismatch, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Mathematically_equivalent_integer_enum_literals_match()
    {
        var handler = new CapturingHandler("{\"value\":1e0}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.integer-enum.v1"] = BuildSinglePropertySchema(
                "value",
                "{\"type\":\"integer\",\"enum\":[1.0]}"),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.integer-enum.v1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("{\"type\":\"integer\"}", "-1e-100")]
    [InlineData("{\"type\":\"integer\",\"enum\":[0]}", "-1e-100")]
    [InlineData("{\"type\":\"number\",\"minimum\":0}", "-1e-100")]
    public async Task Tiny_nonzero_numbers_cannot_underflow_through_local_schema_validation(
        string propertySchema,
        string number)
    {
        var handler = new CapturingHandler($"{{\"value\":{number}}}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.exact-number.v1"] = BuildSinglePropertySchema("value", propertySchema),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.exact-number.v1"), CancellationToken.None);

        Assert.Equal(InferenceOutcome.SchemaMismatch, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("1e29")]
    [InlineData("1e100")]
    public async Task Large_mathematical_integers_match_without_decimal_overflow(string number)
    {
        var handler = new CapturingHandler($"{{\"value\":{number}}}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.large-integer.v1"] = BuildSinglePropertySchema("value", "{\"type\":\"integer\"}"),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.large-integer.v1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1e0")]
    public async Task Integer_valued_array_bounds_are_accepted(string minimum)
    {
        var handler = new CapturingHandler("{\"values\":[1]}");
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.array-bound.v1"] = BuildSinglePropertySchema(
                "values",
                $"{{\"type\":\"array\",\"items\":{{\"type\":\"integer\"}},\"minItems\":{minimum}}}"),
        });

        var result = await Provider(handler, registry)
            .CompleteAsync(Previewed("schema.array-bound.v1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task A_provider_without_a_registry_does_not_advertise_structured_output()
    {
        using var provider = Provider(new CapturingHandler(), registry: null);

        var capabilities = await provider.GetCapabilitiesAsync(CancellationToken.None);

        Assert.False(capabilities.SupportsStructuredOutput);
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

    private static string BuildSinglePropertySchema(string propertyName, string propertySchema)
        => BuildObjectSchema([(propertyName, propertySchema)]);

    private static string BuildObjectSchema(IEnumerable<(string Name, string Schema)> properties)
    {
        var declared = properties.ToArray();
        return "{\"type\":\"object\",\"properties\":{"
            + string.Join(',', declared.Select(property =>
                JsonSerializer.Serialize(property.Name) + ":" + property.Schema))
            + "},\"required\":["
            + string.Join(',', declared.Select(property => JsonSerializer.Serialize(property.Name)))
            + "],\"additionalProperties\":false}";
    }

    private static string BuildStringEnum(int valueCount, int valueLength)
    {
        var values = Enumerable.Range(0, valueCount)
            .Select(index => index.ToString("D4", CultureInfo.InvariantCulture)
                + new string('v', valueLength - 4));
        return "{\"type\":\"string\",\"enum\":["
            + string.Join(',', values.Select(value => JsonSerializer.Serialize(value)))
            + "]}";
    }

    private static async Task AssertSchemaRefusedBeforeAuthOrHttp(string schemaJson)
    {
        var handler = new CapturingHandler();
        var tokenRequests = 0;
        var registry = new InMemorySchemaRegistry(new Dictionary<string, string>
        {
            ["schema.provider-budget.v1"] = schemaJson,
        });
        using var provider = Provider(handler, registry, _ =>
        {
            tokenRequests++;
            return Task.FromResult("token");
        });

        var result = await provider.CompleteAsync(
            Previewed("schema.provider-budget.v1"),
            CancellationToken.None);

        Assert.Equal(InferenceOutcome.UnsupportedCapability, result.Outcome);
        Assert.Equal(0, tokenRequests);
        Assert.Equal(0, handler.RequestCount);
        Assert.Null(handler.LastBody);
    }
}
