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
                  "cloudInferenceEnabled": true
                }
                """);

            var provider = new JsonDistrictPolicyProvider(directory);

            Assert.True(provider.Current.CloudInferenceEnabled);
            Assert.Equal(DataLane.Amber, provider.Current.MaximumLane);
            Assert.Equal("azure-openai", provider.Current.ProviderId);
            Assert.Equal(["https://district.example/openai"], provider.Current.AllowedEndpoints);
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
