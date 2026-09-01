// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text.Json;

namespace Foundry.Tests.Unit;

public sealed class LoadReproEvidenceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string EvidenceModulePath = Path.Combine(
        RepositoryRoot,
        "tools",
        "LoadReproEvidence.psm1");

    [Fact]
    public void Shared_evidence_lock_blocks_a_second_harness_and_can_be_reacquired_after_release()
    {
        var repository = CreateTemporaryRepository("lock");
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $first = Enter-LoadReproEvidenceLock `
                    -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                    -RunId "first" `
                    -HarnessName "headed"
                $secondWasBlocked = $false
                $second = $null
                try {
                    $second = Enter-LoadReproEvidenceLock `
                        -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                        -RunId "second" `
                        -HarnessName "image"
                }
                catch {
                    $secondWasBlocked = $true
                }
                finally {
                    if ($null -ne $second) { $second.Stream.Dispose() }
                }

                $relativePath = $first.RelativePath
                $first.Stream.Dispose()
                $third = Enter-LoadReproEvidenceLock `
                    -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                    -RunId "third" `
                    -HarnessName "image"
                $third.Stream.Dispose()

                [ordered]@{
                    SecondWasBlocked = $secondWasBlocked
                    Reacquired = $true
                    RelativePath = $relativePath
                } | ConvertTo-Json -Compress
                """);

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.True(result.RootElement.GetProperty("SecondWasBlocked").GetBoolean());
            Assert.True(result.RootElement.GetProperty("Reacquired").GetBoolean());
            Assert.Equal(
                Path.Combine("out", ".load-repro-evidence.lock"),
                result.RootElement.GetProperty("RelativePath").GetString());
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_directory_creator_builds_a_checked_unique_hierarchy_and_refuses_reuse()
    {
        var repository = CreateTemporaryRepository("evidence-directory");
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $first = New-LoadReproEvidenceDirectory `
                    -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                    -EvidenceBaseName "image-load-repro" `
                    -RunId "synthetic-run"
                $reuseWasBlocked = $false
                try {
                    New-LoadReproEvidenceDirectory `
                        -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                        -EvidenceBaseName "image-load-repro" `
                        -RunId "synthetic-run" |
                        Out-Null
                }
                catch {
                    $reuseWasBlocked = $true
                }
                [ordered]@{
                    Exists = Test-Path -LiteralPath $first.Path -PathType Container
                    ReuseWasBlocked = $reuseWasBlocked
                    RelativePath = $first.RelativePath
                } | ConvertTo-Json -Compress
                """);

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.True(result.RootElement.GetProperty("Exists").GetBoolean());
            Assert.True(result.RootElement.GetProperty("ReuseWasBlocked").GetBoolean());
            Assert.Equal(
                Path.Combine("out", "image-load-repro", "synthetic-run"),
                result.RootElement.GetProperty("RelativePath").GetString());
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_directory_creator_refuses_a_redirected_base_without_writing_outside()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repository = CreateTemporaryRepository("redirected-evidence-directory");
        var outside = CreateTemporaryRepository("redirected-evidence-target");
        var outRoot = Path.Combine(repository, "out");
        var redirectedBase = Path.Combine(outRoot, "image-load-repro");
        Directory.CreateDirectory(outRoot);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(redirectedBase, outside);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return;
            }

            var process = RunPowerShell(
                repository,
                """
                $refused = $false
                try {
                    New-LoadReproEvidenceDirectory `
                        -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT `
                        -EvidenceBaseName "image-load-repro" `
                        -RunId "redirected-run" |
                        Out-Null
                }
                catch {
                    $refused = $true
                }
                [ordered]@{
                    Refused = $refused
                    OutsideRunExists = Test-Path -LiteralPath `
                        (Join-Path $env:OCF_TEST_OUTSIDE_ROOT "redirected-run")
                } | ConvertTo-Json -Compress
                """,
                new Dictionary<string, string?>
                {
                    ["OCF_TEST_OUTSIDE_ROOT"] = outside,
                });

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.True(result.RootElement.GetProperty("Refused").GetBoolean());
            Assert.False(result.RootElement.GetProperty("OutsideRunExists").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(redirectedBase))
            {
                Directory.Delete(redirectedBase);
            }

            Directory.Delete(repository, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Bounded_taskkill_records_a_settled_nonzero_request_without_native_error_policy_interference()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repository = CreateTemporaryRepository("bounded-taskkill");
        var resultPath = Path.Combine(repository, "taskkill-result.json");
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $PSNativeCommandUseErrorActionPreference = $true
                $result = Invoke-LoadReproBoundedTaskKill `
                    -TargetProcessId 2147483646 `
                    -LimitMilliseconds 10000 `
                    -CleanupLimitMilliseconds 2000
                $result |
                    ConvertTo-Json -Compress |
                    Set-Content -LiteralPath `
                        (Join-Path $env:OCF_TEST_REPOSITORY_ROOT "taskkill-result.json") `
                        -Encoding utf8
                """);

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(File.ReadAllBytes(resultPath));
            Assert.True(result.RootElement.GetProperty("Started").GetBoolean());
            Assert.False(result.RootElement.GetProperty("TimedOut").GetBoolean());
            Assert.NotEqual(0, result.RootElement.GetProperty("ExitCode").GetInt32());
            Assert.True(result.RootElement.GetProperty("HelperExitObserved").GetBoolean());
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("StartError").ValueKind);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Output_identity_is_sorted_and_detects_a_same_length_dependency_mutation()
    {
        var repository = CreateTemporaryRepository("output");
        var outputRoot = Path.Combine(repository, "tests", "Synthetic", "bin", "Release", "net10.0");
        var nestedRoot = Path.Combine(outputRoot, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(nestedRoot);
        var assemblyPath = Path.Combine(outputRoot, "Synthetic.Tests.dll");
        var dependencyPath = Path.Combine(nestedRoot, "dependency.dll");
        File.WriteAllBytes(assemblyPath, [1, 2, 3, 4]);
        File.WriteAllBytes(dependencyPath, [5, 6, 7, 8]);

        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                $outputRoot = Join-Path $root "tests/Synthetic/bin/Release/net10.0"
                $assembly = Join-Path $outputRoot "Synthetic.Tests.dll"
                $dependency = Join-Path $outputRoot "runtimes/win-x64/native/dependency.dll"
                $before = Get-LoadReproOutputIdentity `
                    -RepositoryRoot $root `
                    -OutputRoot $outputRoot `
                    -RequiredTestAssembly $assembly
                [IO.File]::WriteAllBytes($dependency, [byte[]](8, 7, 6, 5))
                $after = Get-LoadReproOutputIdentity `
                    -RepositoryRoot $root `
                    -OutputRoot $outputRoot `
                    -RequiredTestAssembly $assembly
                $source = [pscustomobject]@{
                    Commit = ("a" * 40)
                    Dirty = $false
                    StatusEntryCount = 0
                    StatusSha256 = "status"
                    SourceFileCount = 1
                    SourceContentSha256 = "source"
                }
                $errors = @(Get-LoadReproIdentityErrors `
                    -RepositoryBefore $source `
                    -RepositoryAfter $source `
                    -OutputBefore $before `
                    -OutputAfter $after)
                [ordered]@{
                    BeforeHash = $before.ManifestSha256
                    AfterHash = $after.ManifestSha256
                    BeforeCount = $before.FileCount
                    AfterCount = $after.FileCount
                    BeforeBytes = $before.TotalBytes
                    AfterBytes = $after.TotalBytes
                    Paths = @($before.Files | ForEach-Object Path)
                    Errors = $errors
                } | ConvertTo-Json -Depth 4 -Compress
                """);

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.NotEqual(
                result.RootElement.GetProperty("BeforeHash").GetString(),
                result.RootElement.GetProperty("AfterHash").GetString());
            Assert.Equal(
                result.RootElement.GetProperty("BeforeCount").GetInt32(),
                result.RootElement.GetProperty("AfterCount").GetInt32());
            Assert.Equal(
                result.RootElement.GetProperty("BeforeBytes").GetInt64(),
                result.RootElement.GetProperty("AfterBytes").GetInt64());
            Assert.Equal(
                ["Synthetic.Tests.dll", "runtimes/win-x64/native/dependency.dll"],
                [.. result.RootElement.GetProperty("Paths").EnumerateArray().Select(path => path.GetString()!)]);
            var error = Assert.Single(result.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Contains("test-output/dependency identity changed", error.GetString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Durable_json_writer_is_create_new_utf8_without_bom_and_refuses_overwrite()
    {
        var repository = CreateTemporaryRepository("json");
        var evidenceRoot = Path.Combine(repository, "out", "probe");
        Directory.CreateDirectory(evidenceRoot);
        var summaryPath = Path.Combine(evidenceRoot, "summary.json");
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "out/probe"
                $path = Join-Path $root "summary.json"
                Write-LoadReproJsonFile `
                    -Value ([ordered]@{ Result = "retained" }) `
                    -Path $path `
                    -ContainmentRoot $root `
                    -Description "synthetic summary"
                $overwriteWasBlocked = $false
                try {
                    Write-LoadReproJsonFile `
                        -Value ([ordered]@{ Result = "overwritten" }) `
                        -Path $path `
                        -ContainmentRoot $root `
                        -Description "synthetic summary"
                }
                catch {
                    $overwriteWasBlocked = $true
                }
                [ordered]@{ OverwriteWasBlocked = $overwriteWasBlocked } |
                    ConvertTo-Json -Compress
                """);

            AssertSuccessful(process);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.True(result.RootElement.GetProperty("OverwriteWasBlocked").GetBoolean());

            var bytes = File.ReadAllBytes(summaryPath);
            Assert.True(bytes.Length > 3);
            Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            using var summary = JsonDocument.Parse(bytes);
            Assert.Equal("retained", summary.RootElement.GetProperty("Result").GetString());
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    private static string CreateTemporaryRepository(string suffix)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"ocf-load-repro-evidence-{suffix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static PowerShellResult RunPowerShell(
        string repository,
        string script,
        IReadOnlyDictionary<string, string?>? additionalEnvironment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            "$ErrorActionPreference = 'Stop'; " +
            "Import-Module -Name $env:OCF_TEST_LOAD_EVIDENCE_MODULE -Force; " +
            script);
        startInfo.Environment["OCF_TEST_LOAD_EVIDENCE_MODULE"] = EvidenceModulePath;
        startInfo.Environment["OCF_TEST_REPOSITORY_ROOT"] = repository;
        if (additionalEnvironment is not null)
        {
            foreach (var (name, value) in additionalEnvironment)
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "The PowerShell load-evidence fixture process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(30_000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        Assert.True(exited, "The PowerShell load-evidence fixture process exceeded 30 seconds.");
        return new PowerShellResult(process.ExitCode, output, error);
    }

    private static void AssertSuccessful(PowerShellResult result)
        => Assert.True(
            result.ExitCode == 0,
            $"PowerShell load-evidence fixture failed with exit {result.ExitCode}:" +
            $"{Environment.NewLine}{result.StandardError}");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root for load-evidence tests.");
    }

    private sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError);
}
