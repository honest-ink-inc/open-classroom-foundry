// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

public class PublishScriptContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string UnsignedScript = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "publish.ps1"));
    private static readonly string FinalizerScript = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "finalize-signed-package.ps1"));
    private static readonly string BuildProperties = File.ReadAllText(
        Path.Combine(RepositoryRoot, "Directory.Build.props"));

    [Fact]
    public void Unsigned_script_declares_exact_release_locked_restore_and_no_implicit_restore()
    {
        Assert.Contains("[ValidateSet(\"Release\")]", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains(
            "Unsigned pre-sign packaging accepts only the exact Release configuration.",
            UnsignedScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet restore $applicationProject --runtime win-x64 --locked-mode -p:NuGetLockFilePath=packages.win-x64.lock.json --configfile (Join-Path $repositoryRoot \"NuGet.config\")",
            UnsignedScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet publish $applicationProject -c Release --runtime win-x64 --self-contained false --no-restore",
            UnsignedScript,
            StringComparison.Ordinal);
        AssertAppearsInOrder(
            UnsignedScript,
            "dotnet restore $applicationProject",
            "dotnet publish $applicationProject");
    }

    [Fact]
    public void Unsigned_script_fails_closed_on_source_product_and_dependency_identity()
    {
        Assert.Contains("status\", \"--porcelain=v1\", \"--untracked-files=all", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Compiled ProductVersion does not equal EngineIdentity plus the exact source commit.", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Foundry.App.WinForms/$EngineVersion", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Compiled dependency identity does not equal EngineIdentity.", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("$expectedProductVersion = \"$EngineVersion+$SourceCommit\"", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("accepts only unsigned first-party inputs", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("A release PDB lacked the canonical Honest Ink SourceLink mapping.", UnsignedScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsigned_script_segregates_symbols_and_names_every_output_as_pre_sign()
    {
        Assert.Contains("$stagedSymbols = Join-Path $stagedBundle \"symbols\"", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Release compilation produced no bounded first-party symbol set.", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $symbol.FullName -Destination $symbolDestination", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("$stagedPayload = Join-Path $stagedBundle \"payload\"", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("A PDB remained in the unsigned pre-sign payload.", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("honest-ink-win-x64-unsigned-pre-sign.zip", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("honest-ink-win-x64-unsigned-pre-sign-symbols.zip", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("honest-ink-win-x64-unsigned-pre-sign-symbols.zip.sha256", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.pre-sign.txt", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("pre-sign-manifest.json", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("$zipDigest", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("Built unsigned pre-sign inputs", UnsignedScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Published to out", UnsignedScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("distributable", UnsignedScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pre_sign_manifest_structurally_binds_commit_version_hashes_and_signing_roles()
    {
        Assert.Contains("schemaVersion = 1", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("state = \"unsigned-pre-sign\"", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("sourceCommit = $headCommit", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("engineVersion = $engineVersion", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("sha256 = (Get-FileHash", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("first-party-authenticode", UnsignedScript, StringComparison.Ordinal);
        Assert.Contains("role = if ($isFirstParty)", UnsignedScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Finalizer_structurally_requires_exact_inventory_authorized_signatures_and_signed_tag()
    {
        Assert.Contains("[Parameter(Mandatory)]", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$AllowedSignerThumbprint", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$signature.Status.ToString() -cne \"Valid\"", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$signature.SignatureType.ToString() -cne \"Authenticode\"", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$signature.TimeStamperCertificate", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("outside the authorized thumbprint set", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$expectedTag = \"v$engineVersion\"", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("verify-tag --raw $ExpectedTag", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$ExpectedTag^{}", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("A non-signable file changed after the pre-sign build.", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("remained byte-identical to its unsigned input", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$RelativePath -match '[\\x00-\\x1F\\x7F<>:\"|?*]'", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains(
            "Signed input must be a separate copy outside unsigned evidence",
            FinalizerScript,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Finalizer_structurally_regenerates_signed_hashes_and_hashes_the_final_zip()
    {
        Assert.Contains("SHA256SUMS.txt", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("honest-ink-win-x64-$engineVersion-signed.zip", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$zipName.sha256", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("$zipDigest = (Get-FileHash -LiteralPath $stagedZip", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("state = \"signed-final\"", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("signedTag = $expectedTag", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("signerThumbprints", FinalizerScript, StringComparison.Ordinal);
        Assert.Contains("signed-release-manifest.json", FinalizerScript, StringComparison.Ordinal);
        AssertAppearsInOrder(
            FinalizerScript,
            "Get-AuthenticodeSignature -LiteralPath $file.FullName",
            "Set-Content -LiteralPath $stagedSums",
            "Compress-Archive -Path (Join-Path $stagedPayload \"*\")");
    }

    [Fact]
    public void Both_scripts_retain_one_cooperative_lock_and_rollback_recovery_structure()
    {
        foreach (var script in new[] { UnsignedScript, FinalizerScript })
        {
            Assert.Contains("return ,$result", script, StringComparison.Ordinal);
            Assert.Contains("$packageLockPath = Join-Path $packageRoot \".package.lock\"", script, StringComparison.Ordinal);
            Assert.Contains("[IO.FileShare]::None", script, StringComparison.Ordinal);
            Assert.Contains("Another packaging process already holds the package-root lock.", script, StringComparison.Ordinal);
            Assert.Contains("Move-Item -LiteralPath $finalBundle -Destination $backupBundle", script, StringComparison.Ordinal);
            Assert.Contains("Move-Item -LiteralPath $backupBundle -Destination $finalBundle", script, StringComparison.Ordinal);
            Assert.Contains("rollback was incomplete", script, StringComparison.Ordinal);
            Assert.Contains("$packageLock.Dispose()", script, StringComparison.Ordinal);
        }

        AssertLastOccurrenceBefore(
            UnsignedScript,
            "Assert-CleanSourceIdentity $headCommit",
            "Move-Item -LiteralPath $stagedBundle -Destination $finalBundle");
        AssertLastOccurrenceBefore(
            FinalizerScript,
            "Assert-CleanTaggedSource $sourceCommit $expectedTag",
            "Move-Item -LiteralPath $stagedBundle -Destination $finalBundle");
    }

    [Fact]
    public void Equivalent_file_lock_rejects_a_concurrent_handle_and_can_be_reacquired()
    {
        var root = Path.Combine(Path.GetTempPath(), $"foundry-package-lock-{Guid.NewGuid():N}");
        var lockPath = Path.Combine(root, ".package.lock");
        Directory.CreateDirectory(root);

        try
        {
            using (File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<IOException>(() =>
                {
                    using var competing = File.Open(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                });
            }

            using var reacquired = File.Open(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            Assert.True(reacquired.CanWrite);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scripts_contain_no_known_command_for_an_outward_typist_act()
    {
        var forbidden = new[]
        {
            @"\bSet-AuthenticodeSignature\b",
            @"\bsigntool(?:\.exe)?\b",
            @"\bgit\s+tag\b",
            @"\bgh\s+release\b",
            @"\bInvoke-WebRequest\b",
            @"\bInvoke-RestMethod\b",
            @"\bStart-Process\b",
            @"\bmsiexec(?:\.exe)?\b",
            @"\bwinget(?:\.exe)?\b",
            @"\bdeploy-pages\b",
        };

        foreach (var script in new[] { UnsignedScript, FinalizerScript })
        {
            foreach (var pattern in forbidden)
            {
                Assert.DoesNotMatch(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), script);
            }
        }
    }

    [Fact]
    public void Build_metadata_declares_canonical_source_mapping_without_changing_version()
    {
        Assert.Contains("<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>", BuildProperties, StringComparison.Ordinal);
        Assert.Contains("<DeterministicSourcePaths>true</DeterministicSourcePaths>", BuildProperties, StringComparison.Ordinal);
        Assert.Contains("<PathMap>$(MSBuildThisFileDirectory)=/_/</PathMap>", BuildProperties, StringComparison.Ordinal);
        Assert.Contains("<RepositoryUrl>https://github.com/honest-ink-inc/open-classroom-foundry</RepositoryUrl>", BuildProperties, StringComparison.Ordinal);
        Assert.DoesNotContain("<Version>", BuildProperties, StringComparison.Ordinal);
        Assert.DoesNotContain("<AssemblyVersion>", BuildProperties, StringComparison.Ordinal);
        Assert.DoesNotContain("<FileVersion>", BuildProperties, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiled_test_evidence_has_canonical_sourcelink_and_no_local_repository_path()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        Assert.True(File.Exists(pdbPath), "The Release test build must retain separate symbol evidence.");

        var assemblyText = Encoding.UTF8.GetString(File.ReadAllBytes(assemblyPath));
        var pdbText = Encoding.UTF8.GetString(File.ReadAllBytes(pdbPath));
        Assert.DoesNotContain(RepositoryRoot, assemblyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RepositoryRoot, pdbText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw.githubusercontent.com/Spacejunk-io/open-classroom-foundry", pdbText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw.githubusercontent.com/honest-ink-inc/open-classroom-foundry", pdbText, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for packaging contract tests.");
    }

    private static void AssertAppearsInOrder(string source, params string[] fragments)
    {
        var offset = 0;
        foreach (var fragment in fragments)
        {
            var index = source.IndexOf(fragment, offset, StringComparison.Ordinal);
            Assert.True(index >= offset, $"Expected fragment after offset {offset}: {fragment}");
            offset = index + fragment.Length;
        }
    }

    private static void AssertLastOccurrenceBefore(string source, string before, string after)
    {
        var beforeIndex = source.LastIndexOf(before, StringComparison.Ordinal);
        var afterIndex = source.LastIndexOf(after, StringComparison.Ordinal);
        Assert.True(beforeIndex >= 0 && beforeIndex < afterIndex, $"Expected final '{before}' before final '{after}'.");
    }
}
