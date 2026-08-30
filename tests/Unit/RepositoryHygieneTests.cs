// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace Foundry.Tests.Unit;

/// <summary>
/// The machine half of the one rule that outranks everything (CONTRIBUTING.md).
///
/// Written 30 Aug 2026 after a near-miss: the seeded-error study's answer key
/// sat in this repository and was briefly public. Nothing was lost, but nothing
/// had *prevented* it either — the guard was a person choosing to look. Prose
/// asks contributors to be careful; this asks git.
///
/// It asserts over what git TRACKS, not over what happens to sit in the working
/// directory, because the claim is about what a clone or a publish exposes. A
/// developer's own ignored `.env` is their business; a committed one is
/// everyone's.
/// </summary>
public class RepositoryHygieneTests
{
    /// <summary>Names that are never legitimate in this repository, whatever directory they appear in.</summary>
    private static readonly string[] ForbiddenNames =
    [
        "FACILITATOR-KEY.md",   // the seeded-error answer key: study-fatal, not security-fatal
        "seeded-packets.json",  // the study definitions: an answer key by another name
        ".env",
        "secrets.json",
        "id_rsa",
        "id_ed25519",
    ];

    /// <summary>Extensions that carry private key material by convention.</summary>
    private static readonly string[] ForbiddenExtensions = [".pem", ".key", ".pfx", ".p12", ".publishsettings"];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root by walking up to OpenClassroomFoundry.slnx.");
    }

    private static List<string> TrackedFiles()
    {
        var start = new ProcessStartInfo("git", "ls-files")
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start git; this test asserts over tracked files and git is the only authority on those.");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'git ls-files' failed with exit code {process.ExitCode}. The hygiene guard fails loudly rather than passing blind.");
        }

        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())];
    }

    [Fact]
    public void No_credential_or_answer_key_is_tracked_anywhere_in_the_repository()
    {
        var offenders = TrackedFiles()
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return ForbiddenNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                    || ForbiddenExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
            })
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These tracked files are credentials or study answer keys and must never be committed — "
            + "remove them from the index AND from history, and rotate anything that was ever a real credential: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void The_pilot_kit_tracks_its_fictional_example_and_nothing_generated()
    {
        // The kit's own contract: definitions and packets are OUTPUT, generated
        // from a file the facilitator keeps outside this repository. Only the
        // obviously-fictional example belongs here.
        var tracked = TrackedFiles()
            .Where(p => p.StartsWith("docs/evidence/pilot-kit/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Contains("docs/evidence/pilot-kit/seeded-packets.example.json", tracked, StringComparer.OrdinalIgnoreCase);

        var generated = tracked.Where(p => Path.GetFileName(p).StartsWith("packet-", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(
            generated.Count == 0,
            "Generated study packets are tracked. Committing them lets a participant study the instrument before sitting the study: "
            + string.Join(", ", generated));
    }

    [Fact]
    public void The_secret_scanning_guards_are_still_installed()
    {
        // A guard that is quietly deleted is worse than one that never existed,
        // because the documents still claim it. This asserts the machinery exists.
        var root = RepoRoot();

        Assert.True(File.Exists(Path.Combine(root, ".githooks", "pre-commit")),
            "The pre-commit secret-scan hook is missing; CONTRIBUTING.md and AGENTS.md both promise it.");

        Assert.True(File.Exists(Path.Combine(root, "AGENTS.md")),
            "AGENTS.md is canonical for automated contributors and is referenced by CONTRIBUTING.md and README.md.");

        // Claude Code loads CLAUDE.md, Codex loads AGENTS.md. A missing pointer
        // means the tool that wrote most of this repository reads no rules at all.
        var claude = Path.Combine(root, "CLAUDE.md");
        Assert.True(File.Exists(claude), "CLAUDE.md is missing; Claude Code would then load no project guidance.");
        Assert.Contains("AGENTS.md", File.ReadAllText(claude), StringComparison.Ordinal);

        var ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        foreach (var pattern in new[] { ".env", "*.pem", "seeded-packets.json", "FACILITATOR-KEY.md" })
        {
            Assert.Contains(pattern, ignore, StringComparison.Ordinal);
        }
    }
}
