// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Tests.Unit;

public class HeadedUiaLoadReproductionHarnessTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string Harness = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "run-headed-uia-load-repro.ps1"));
    private static readonly string HeadedTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "UiAutomation", "HeadedUiaWalkTests.cs"));
    private static readonly string PilotTest = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "UiAutomation", "HeadedUiaWalkTests.PilotRehearsal.cs"));

    [Fact]
    public void Harness_runs_only_the_two_named_sightings_without_changing_the_probe_timeout()
    {
        Assert.Contains(
            "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.PilotDay_dress_rehearsal_cold_start_to_reopened_booklet_and_low_ink_over_real_uia",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("int timeoutMs = 20000", HeadedTests, StringComparison.Ordinal);
        Assert.DoesNotContain("timeoutMs", Harness, StringComparison.Ordinal);
        Assert.Contains("ExistingProbeTimeoutMilliseconds = 20000", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Harness_is_bounded_and_applies_controlled_cpu_and_memory_contention()
    {
        Assert.Contains("[ValidateRange(1, 10)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(1, 8)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(32, 1024)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(60, 600)]", Harness, StringComparison.Ordinal);
        Assert.Contains("Start-ControlledContention", Harness, StringComparison.Ordinal);
        Assert.Contains("$process.WaitForExit($LimitSeconds * 1000)", Harness, StringComparison.Ordinal);
        Assert.Contains("$exitCode = 124", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_repetition_retains_the_named_result_and_content_free_failure_evidence()
    {
        Assert.Equal(1, CountOccurrences(Harness, "\"test\", $testProject,"));
        Assert.Contains("FullyQualifiedName = $testCase.FullyQualifiedName", Harness, StringComparison.Ordinal);
        Assert.Contains("Repetition = $repetition", Harness, StringComparison.Ordinal);
        Assert.Contains("ExitCode = $boundedRun.ExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains("FailureMessage = $failureMessage", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardOutputFile", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardErrorFile", Harness, StringComparison.Ordinal);
        Assert.Contains("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']", Harness, StringComparison.Ordinal);
        Assert.Contains("Content-free diagnostic snapshot", HeadedTests, StringComparison.Ordinal);
        Assert.Contains("no action retry issued", Harness, StringComparison.Ordinal);
        Assert.Contains("non-reproductions, not a diagnosis", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Headed_processes_inherit_only_disposable_test_storage()
    {
        Assert.Contains("$env:TEMP = $scratchRoot", Harness, StringComparison.Ordinal);
        Assert.Contains("$env:TMP = $scratchRoot", Harness, StringComparison.Ordinal);
        Assert.Contains("ocf-rehearsal-", PilotTest, StringComparison.Ordinal);
        Assert.Contains("ProjectLibraryRootConfiguration.Switch", PilotTest, StringComparison.Ordinal);
        Assert.Contains("The library lives in a disposable directory", PilotTest, StringComparison.Ordinal);
        Assert.DoesNotContain("Documents", Harness, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for headed-load harness tests.");
    }
}
