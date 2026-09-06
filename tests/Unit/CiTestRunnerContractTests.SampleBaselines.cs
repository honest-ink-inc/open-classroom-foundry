// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Foundry.Tests.Unit;

public sealed partial class CiTestRunnerContractTests
{
    [Theory]
    [InlineData("recipe-first-admission-samples.sha256")]
    [InlineData("engine-0.8.0-alpha-candidate-samples.sha256")]
    public async Task Sample_manifests_pin_actual_Git_checkout_attributes_to_LF(string fileName)
    {
        var relativePath = "tests/Rendering/Fixtures/" + fileName;
        var script = $$"""
            & git -C $env:OCF_TEST_REPOSITORY_ROOT check-attr text eol -- '{{relativePath}}'
            exit $LASTEXITCODE
            """;
        var result = await RunPowerShellAsync(RepositoryRoot, script);
        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Contains(relativePath + ": text: set", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(relativePath + ": eol: lf", result.StandardOutput, StringComparison.Ordinal);

        // Query effective Git attributes as well as current bytes: an LF file
        // created locally can otherwise conceal CRLF conversion on checkout.
        var bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot, relativePath));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    [Theory]
    [InlineData("valid", null)]
    [InlineData("historical-manifest", "sample.historical-manifest")]
    [InlineData("nonpackage", "sample.nonpackage-drift")]
    [InlineData("missing-row", "sample.manifest-count")]
    [InlineData("extra-row", "sample.manifest-count")]
    [InlineData("duplicate-row", "sample.manifest-duplicate")]
    [InlineData("wrong-path", "sample.manifest-paths")]
    [InlineData("unchanged-package", "sample.candidate-package-unchanged")]
    [InlineData("historical-package", "sample.historical-package")]
    [InlineData("writer", "sample.package-writer")]
    [InlineData("manifest-extra", "sample.package-manifest")]
    [InlineData("duplicate-writer", "sample.package-writer")]
    [InlineData("snapshot", "sample.package-payload")]
    [InlineData("asset", "sample.package-payload")]
    [InlineData("missing-entry", "sample.package-entries")]
    [InlineData("extra-entry", "sample.package-entries")]
    [InlineData("duplicate-entry", "sample.package-entries")]
    [InlineData("entry-order", "sample.package-entries")]
    [InlineData("entry-stamp", "sample.package-metadata")]
    [InlineData("entry-attributes", "sample.package-metadata")]
    [InlineData("output-inventory", "sample.output-inventory")]
    [InlineData("output-hash", "sample.output-hash")]
    public async Task Sample_baseline_guard_preserves_C1_and_permits_only_the_exact_writer_stamp(
        string mutation,
        string? expectedFailure)
    {
        var fixture = new SampleBaselineFixture(mutation);
        var safeToRemoveFixture = true;
        Exception? primaryFailure = null;
        try
        {
            var guard = Path.Combine(RepositoryRoot, "tools", "verify-sample-baselines.ps1")
                .Replace("'", "''", StringComparison.Ordinal);
            var script = $$"""
                . '{{guard}}'
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                $historical = [IO.File]::ReadAllBytes((Join-Path $root 'historical.sha256'))
                $candidate = [IO.File]::ReadAllBytes((Join-Path $root 'candidate.sha256'))
                $contract = Assert-SampleBaselineContract -HistoricalManifest $historical -CandidateManifest $candidate
                Write-Output "Exact comparison contract: $($contract.Paths.Length) paths; candidate package is separately identified."
                Assert-SamplePackageContract `
                    -HistoricalPackage ([IO.File]::ReadAllBytes((Join-Path $root 'historical.ocfproj'))) `
                    -CandidatePackage ([IO.File]::ReadAllBytes((Join-Path $root 'candidate.ocfproj')))
                Write-Output 'Exact package relation: only the top-level 0.7 to 0.8 writer stamp changed.'
                """;
            if (mutation is "output-inventory" or "output-hash")
            {
                script += """

                    Invoke-SampleBaselineVerification -Root (Join-Path $root 'samples') `
                        -HistoricalPath (Join-Path $root 'historical.sha256') `
                        -CandidatePath (Join-Path $root 'candidate.sha256') `
                        -PackagePath (Join-Path $root 'historical.base64')
                    """;
            }

            var result = await RunPowerShellAsync(fixture.Root, script);
            if (expectedFailure is null)
            {
                Assert.True(result.ExitCode == 0, result.StandardError);
                Assert.Contains("Exact comparison contract: 40 paths", result.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("only the top-level 0.7 to 0.8 writer stamp changed", result.StandardOutput, StringComparison.Ordinal);
            }
            else
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains(expectedFailure, result.StandardError, StringComparison.Ordinal);
            }
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            if (exception is FixtureProcessException processFailure)
            {
                safeToRemoveFixture = processFailure.Result.SafeToStartAnotherFixture;
            }
            throw;
        }
        finally
        {
            if (safeToRemoveFixture)
            {
                try
                {
                    fixture.Dispose();
                }
                catch (Exception cleanupFailure) when (primaryFailure is not null
                    && cleanupFailure is IOException or UnauthorizedAccessException)
                {
                    testOutput.WriteLine("Synthetic fixture cleanup failed after the preserved primary failure: " + cleanupFailure);
                }
            }
            else
            {
                testOutput.WriteLine("Retained synthetic sample fixture because process ownership remains uncertain: " + fixture.Root);
            }
        }
    }

    // This constructs a labeled comparison control from exact synthetic C1
    // fixture bytes. It is not execution of the 0.8 writer or a replacement for
    // the all-40-file generator gate. In particular it invents no hash preimages
    // for the unchanged 39 sample files.
    private sealed class SampleBaselineFixture : IDisposable
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ocf-sample-baseline-" + Guid.NewGuid().ToString("N"));

        public SampleBaselineFixture(string mutation)
        {
            var historicalManifest = File.ReadAllBytes(Path.Combine(
                RepositoryRoot, "tests", "Rendering", "Fixtures", "recipe-first-admission-samples.sha256"));
            var historicalPackage = Convert.FromBase64String(File.ReadAllText(Path.Combine(
                RepositoryRoot, "tests", "Integration", "Fixtures", "upgrade", "c1-first-admission-task-strip.ocfproj.base64")));
            Assert.Equal(3338, historicalPackage.Length);
            Assert.Equal("9014BFFB477FC9F470E3901393AF1B6D34ACF697B5E0CD174A12BF8D76A74C82", Hash(historicalPackage));
            var candidatePackage = CandidatePackage(historicalPackage, mutation);
            var lines = Utf8.GetString(historicalManifest).TrimEnd('\n').Split('\n').ToList();
            var packageIndex = lines.FindIndex(line => line.StartsWith("task-strip-bilingual.ocfproj ", StringComparison.Ordinal));
            Assert.True(packageIndex >= 0);
            lines[packageIndex] = "task-strip-bilingual.ocfproj " + Hash(candidatePackage);
            switch (mutation)
            {
                case "historical-manifest":
                    historicalManifest[0] ^= 1;
                    break;
                case "nonpackage":
                    lines[0] = "agency-cards.learner.html " + new string('A', 64);
                    break;
                case "missing-row":
                    lines.RemoveAt(0);
                    break;
                case "extra-row":
                    lines.Add("zz-extra.html " + new string('A', 64));
                    break;
                case "duplicate-row":
                    lines[1] = lines[0];
                    break;
                case "wrong-path":
                    lines[0] = lines[0].Replace("agency-cards", "agency-cardz", StringComparison.Ordinal);
                    break;
                case "unchanged-package":
                    lines[packageIndex] = "task-strip-bilingual.ocfproj " + Hash(historicalPackage);
                    break;
                case "historical-package":
                    historicalPackage[^1] ^= 1;
                    break;
            }

            Directory.CreateDirectory(Root);
            File.WriteAllBytes(Path.Combine(Root, "historical.sha256"), historicalManifest);
            File.WriteAllText(Path.Combine(Root, "candidate.sha256"), string.Join('\n', lines) + "\n", Utf8);
            File.WriteAllBytes(Path.Combine(Root, "historical.ocfproj"), historicalPackage);
            File.WriteAllBytes(Path.Combine(Root, "candidate.ocfproj"), candidatePackage);
            File.WriteAllText(Path.Combine(Root, "historical.base64"), Convert.ToBase64String(historicalPackage), Utf8);
            if (mutation is "output-inventory" or "output-hash")
            {
                var samples = Path.Combine(Root, "samples");
                Directory.CreateDirectory(samples);
                if (mutation == "output-hash")
                {
                    foreach (var line in lines)
                    {
                        File.WriteAllBytes(Path.Combine(samples, line[..line.IndexOf(' ')]), []);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

        private static byte[] CandidatePackage(byte[] historical, string mutation)
        {
            using var source = new MemoryStream(historical, writable: false);
            using var sourceZip = new ZipArchive(source, ZipArchiveMode.Read);
            using var destination = new MemoryStream();
            using (var candidate = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entries = sourceZip.Entries.ToArray();
                if (mutation == "entry-order")
                {
                    Array.Reverse(entries);
                }
                for (var index = 0; index < entries.Length; index++)
                {
                    var entry = entries[index];
                    if (mutation == "missing-entry" && index == entries.Length - 1)
                    {
                        continue;
                    }
                    using var input = entry.Open();
                    using var payload = new MemoryStream();
                    input.CopyTo(payload);
                    var bytes = payload.ToArray();
                    if (entry.FullName == "manifest.json")
                    {
                        var text = Utf8.GetString(bytes).Replace(
                            "\"engineVersion\": \"0.7.0-alpha\"",
                            "\"engineVersion\": \"0.8.0-alpha\"",
                            StringComparison.Ordinal);
                        text = mutation switch
                        {
                            "writer" => text.Replace("0.8.0-alpha", "0.9.0-alpha", StringComparison.Ordinal),
                            "manifest-extra" => text.Replace("\"schemaVersion\": \"1\"", "\"schemaVersion\": \"2\"", StringComparison.Ordinal),
                            "duplicate-writer" => text.Replace("\"engineVersion\": \"0.8.0-alpha\"", "\"engineVersion\": \"0.8.0-alpha\",\r\n  \"engineVersion\": \"0.8.0-alpha\"", StringComparison.Ordinal),
                            _ => text,
                        };
                        bytes = Utf8.GetBytes(text);
                    }
                    if ((mutation == "snapshot" && entry.FullName == "snapshot.html")
                        || (mutation == "asset" && entry.FullName.StartsWith("assets/", StringComparison.Ordinal)))
                    {
                        bytes = [.. bytes, (byte)' '];
                    }
                    var name = mutation == "duplicate-entry" && index == entries.Length - 1
                        ? entries[0].FullName
                        : entry.FullName;
                    var copied = candidate.CreateEntry(name, CompressionLevel.Optimal);
                    copied.LastWriteTime = mutation == "entry-stamp" ? entry.LastWriteTime.AddSeconds(2) : entry.LastWriteTime;
                    copied.ExternalAttributes = mutation == "entry-attributes" ? 1 : entry.ExternalAttributes;
                    using var output = copied.Open();
                    output.Write(bytes);
                }
                if (mutation == "extra-entry")
                {
                    candidate.CreateEntry("extra.txt");
                }
            }
            return destination.ToArray();
        }
    }
}
