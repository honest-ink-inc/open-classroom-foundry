using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

public class PolicyStoreTests
{
    private static string FreshDirectory()
        => Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));

    private static void Cleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private static void WritePolicy(string directory, string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, JsonDistrictPolicyProvider.PolicyFileName),
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void AssertOffline(DistrictPolicy policy)
    {
        Assert.False(policy.CloudInferenceEnabled);
        Assert.Equal(DataLane.Green, policy.MaximumLane);
        Assert.Empty(policy.AllowedEndpoints);
        Assert.Null(policy.ProviderId);
        Assert.Null(policy.DeploymentId);
    }

    [Fact]
    public void A_missing_policy_fails_closed_to_offline()
    {
        var directory = FreshDirectory();
        try
        {
            var provider = new JsonDistrictPolicyProvider(directory);

            Assert.False(provider.Current.CloudInferenceEnabled);
            Assert.Equal(DataLane.Green, provider.Current.MaximumLane);
            Assert.Empty(provider.Current.AllowedEndpoints);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_corrupt_policy_fails_closed_to_offline()
    {
        var directory = FreshDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, JsonDistrictPolicyProvider.PolicyFileName), "{ not json");

            var provider = new JsonDistrictPolicyProvider(directory);

            Assert.False(provider.Current.CloudInferenceEnabled);
            Assert.Empty(provider.Current.AllowedEndpoints);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void An_oversized_policy_fails_closed_without_being_materialized()
    {
        var directory = FreshDirectory();
        try
        {
            Assert.Equal(64 * 1024, JsonDistrictPolicyProvider.MaximumPolicyFileBytes);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                Path.Combine(directory, JsonDistrictPolicyProvider.PolicyFileName),
                [.. Enumerable.Repeat((byte)' ', JsonDistrictPolicyProvider.MaximumPolicyFileBytes + 1)]);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_duplicate_policy_property_fails_closed()
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                """
                {
                  "allowedEndpoints": ["https://district.example"],
                  "providerId": "azure-openai",
                  "deploymentId": "district-gpt",
                  "maximumLane": "green",
                  "cloudInferenceEnabled": false,
                  "cloudInferenceEnabled": true
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_case_confusable_policy_property_fails_closed()
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                """
                {
                  "allowedEndpoints": ["https://district.example"],
                  "providerId": "azure-openai",
                  "deploymentId": "district-gpt",
                  "maximumLane": "green",
                  "cloudInferenceEnabled": false,
                  "CloudInferenceEnabled": true
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void An_unknown_policy_property_fails_closed()
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                """
                {
                  "allowedEndpoints": [],
                  "providerId": null,
                  "deploymentId": null,
                  "maximumLane": "green",
                  "cloudInferenceEnabled": false,
                  "unreviewedGrant": true
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_policy_missing_a_required_property_fails_closed()
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                """
                {
                  "allowedEndpoints": [],
                  "providerId": null,
                  "maximumLane": "green",
                  "cloudInferenceEnabled": false
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Invalid_utf8_policy_bytes_fail_closed()
    {
        var directory = FreshDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                Path.Combine(directory, JsonDistrictPolicyProvider.PolicyFileName),
                [(byte)'{', (byte)'"', 0xFF, (byte)'"', (byte)':', (byte)'0', (byte)'}']);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_policy_beyond_the_json_depth_limit_fails_closed()
    {
        var directory = FreshDirectory();
        try
        {
            Assert.Equal(8, JsonDistrictPolicyProvider.MaximumPolicyJsonDepth);
            WritePolicy(
                directory,
                """
                {
                  "allowedEndpoints": [[[[[[[[[["https://district.example"]]]]]]]]]],
                  "providerId": "azure-openai",
                  "deploymentId": "district-gpt",
                  "maximumLane": "green",
                  "cloudInferenceEnabled": true
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"Green\"")]
    public void A_policy_lane_must_be_a_string_only_camel_case_value(string laneJson)
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                $$"""
                {
                  "allowedEndpoints": [],
                  "providerId": null,
                  "deploymentId": null,
                  "maximumLane": {{laneJson}},
                  "cloudInferenceEnabled": false
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Theory]
    [InlineData("[]", "\"azure-openai\"", "\"district-gpt\"")]
    [InlineData("[\"https://district.example\"]", "\"   \"", "\"district-gpt\"")]
    [InlineData("[\"https://district.example\"]", "\"azure-openai\"", "null")]
    [InlineData("[null]", "\"azure-openai\"", "\"district-gpt\"")]
    [InlineData("[\"not an endpoint\"]", "\"azure-openai\"", "\"district-gpt\"")]
    [InlineData("[\"http://district.example\"]", "\"azure-openai\"", "\"district-gpt\"")]
    [InlineData("[\"https://user@district.example\"]", "\"azure-openai\"", "\"district-gpt\"")]
    [InlineData("[\"https://district.example\",null]", "\"azure-openai\"", "\"district-gpt\"")]
    public void An_incomplete_enabled_cloud_grant_fails_closed(
        string endpointsJson,
        string providerIdJson,
        string deploymentIdJson)
    {
        var directory = FreshDirectory();
        try
        {
            WritePolicy(
                directory,
                $$"""
                {
                  "allowedEndpoints": {{endpointsJson}},
                  "providerId": {{providerIdJson}},
                  "deploymentId": {{deploymentIdJson}},
                  "maximumLane": "amber",
                  "cloudInferenceEnabled": true
                }
                """);

            AssertOffline(new JsonDistrictPolicyProvider(directory).Current);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void District_policy_loads_what_the_district_granted_and_nothing_more()
    {
        var directory = FreshDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, JsonDistrictPolicyProvider.PolicyFileName),
                """
                {
                  "allowedEndpoints": ["https://district.example/openai"],
                  "providerId": "azure-openai",
                  "deploymentId": "district-gpt",
                  "maximumLane": "amber",
                  "cloudInferenceEnabled": true,
                  "safeguardingProcedureText": "Call the supervising adult."
                }
                """);

            var provider = new JsonDistrictPolicyProvider(directory);

            Assert.True(provider.Current.CloudInferenceEnabled);
            Assert.Equal(DataLane.Amber, provider.Current.MaximumLane);
            Assert.Equal("azure-openai", provider.Current.ProviderId);
            Assert.Equal(["https://district.example/openai"], provider.Current.AllowedEndpoints);
            Assert.Equal("Call the supervising adult.", provider.Current.SafeguardingProcedureText);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Teacher_preferences_default_when_missing_and_round_trip_when_saved()
    {
        var directory = FreshDirectory();
        try
        {
            var store = new JsonTeacherPreferencesStore(directory);

            Assert.Equal(new TeacherPreferences(), store.Load());

            var preferences = new TeacherPreferences("en", "es", DuplexDefault: true, PrintCopiesDefault: 25);
            store.Save(preferences);

            Assert.Equal(preferences, store.Load());
        }
        finally
        {
            Cleanup(directory);
        }
    }
}
