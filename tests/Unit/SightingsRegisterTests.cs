// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

/// <summary>
/// The sightings register is the one place a recorded test-failure sighting is
/// named, dated, and kept open. These tests keep it exhaustive against every
/// governing record, honest about what a passing rerun means, and unable to
/// name a test that does not exist.
/// </summary>
public sealed partial class SightingsRegisterTests
{
    private const string OpenSightingsHeading = "## Open sightings";
    private const string NamedBesideHeading = "## Named beside sightings, not sightings";
    private const string GuardHeading = "## The guard";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string RegisterPath = Path.Combine(RepositoryRoot, "docs", "evidence", "sightings-register.md");
    private static readonly string Register = File.ReadAllText(RegisterPath);

    private sealed record RegisterRow(string Id, IReadOnlyList<string> Cells);

    [GeneratedRegex(@"\b(?<identifier>[A-Za-z]+Tests\.[A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+)\b")]
    private static partial Regex TestIdentifier();

    [GeneratedRegex("console-signal\\.[a-z][a-z-]*[a-z]")]
    private static partial Regex ConsoleSignalToken();

    [Fact]
    public void Every_sighting_named_in_a_governing_record_has_a_register_row()
    {
        var docsRoot = Path.Combine(RepositoryRoot, "docs");
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(RegisterPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (!line.Contains("sighting", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var identifier in Identifiers(line))
                {
                    if (!Register.Contains(identifier, StringComparison.Ordinal))
                    {
                        missing.Add($"{Path.GetRelativePath(RepositoryRoot, file)}:{lineNumber}: {identifier}");
                    }
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "A governing record names a test beside the word 'sighting' that the sightings register does not carry:\n"
            + string.Join('\n', missing));
    }

    [Fact]
    public void Open_rows_are_sequential_complete_and_open_and_named_rows_are_deterministic()
    {
        var openRows = TableRows(Section(Register, OpenSightingsHeading, NamedBesideHeading), "S-");
        Assert.True(openRows.Count >= 11, "The register must carry at least the eleven sightings recorded through 1 September 2026.");
        for (var index = 0; index < openRows.Count; index++)
        {
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"S-{index + 1:D2}"), openRows[index].Id);
        }

        Assert.All(openRows, row =>
        {
            Assert.Equal(5, row.Cells.Count);
            Assert.All(row.Cells, cell => Assert.False(string.IsNullOrWhiteSpace(cell), $"Register row '{row.Id}' has an empty cell."));
            Assert.Equal("Open sighting", row.Cells[^1]);
        });

        var namedRows = TableRows(Section(Register, NamedBesideHeading, GuardHeading), "N-");
        Assert.NotEmpty(namedRows);
        for (var index = 0; index < namedRows.Count; index++)
        {
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"N-{index + 1:D2}"), namedRows[index].Id);
        }

        Assert.All(namedRows, row =>
        {
            Assert.Equal(2, row.Cells.Count);
            Assert.StartsWith("Deterministic, ", row.Cells[^1], StringComparison.Ordinal);
            Assert.Contains("Not a sighting", row.Cells[^1], StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_local_stall_record_distinguishes_configured_caps_and_the_separate_diagnosis()
    {
        var row = TableRows(Section(Register, OpenSightingsHeading, NamedBesideHeading), "S-")
            .Single(candidate => candidate.Id == "S-08");
        var observations = row.Cells[3];

        Assert.Contains("20260904T035103Z-906577fa2b024bd9859cdc1e00936a7f", observations, StringComparison.Ordinal);
        Assert.Contains("900-second outer bound", observations, StringComparison.Ordinal);
        Assert.Contains("20260904T081350Z-c5320441d5ba40f0bef1fdae213c54ea", observations, StringComparison.Ordinal);
        Assert.Contains("1,800-second configured cap", observations, StringComparison.Ordinal);
        Assert.Contains("1,800,227 ms", observations, StringComparison.Ordinal);
        Assert.Contains("separately diagnosed Board-to-Brief episode", observations, StringComparison.Ordinal);
        Assert.Contains("does not diagnose these undumped episodes", observations, StringComparison.Ordinal);
        Assert.Equal("Open sighting", row.Cells[^1]);
    }

    [Fact]
    public void Every_register_identifier_names_a_real_test_and_every_token_exists_in_the_sender()
    {
        var testSources = SourceFiles(Path.Combine(RepositoryRoot, "tests"));
        var identifiers = TestIdentifier().Matches(Register)
            .Select(match => match.Groups["identifier"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(identifiers);
        foreach (var identifier in identifiers)
        {
            var separator = identifier.IndexOf('.', StringComparison.Ordinal);
            var className = identifier[..separator];
            var methodName = identifier[(separator + 1)..];
            var matched = testSources
                .Where(file => Path.GetFileName(file).StartsWith(className + ".", StringComparison.Ordinal))
                .Any(file => File.ReadAllText(file).Contains(methodName + "(", StringComparison.Ordinal));
            Assert.True(matched, $"Register identifier '{identifier}' names no test method in a {className}.*.cs source file.");
        }

        var sender = string.Concat(SourceFiles(Path.Combine(RepositoryRoot, "tools", "ConsoleControlSignalSender")).Select(File.ReadAllText));
        var tokens = ConsoleSignalToken().Matches(Register)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.Contains("console-signal.lock-observation-timeout", tokens);
        Assert.Contains("console-signal.attach-failed", tokens);
        Assert.All(tokens, token => Assert.Contains(token, sender, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_relative_link_in_the_register_resolves_inside_the_repository()
    {
        var registerDirectory = Path.GetDirectoryName(RegisterPath)
            ?? throw new InvalidOperationException("The sightings register has no parent directory.");
        var rootPrefix = RepositoryRoot + Path.DirectorySeparatorChar;
        var destinations = new List<string>();
        var cursor = 0;
        while (true)
        {
            var start = Register.IndexOf("](", cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            start += 2;
            var end = Register.IndexOf(')', start);
            Assert.True(end > start, "The sightings register contains an unterminated Markdown link.");
            destinations.Add(Register[start..end].Trim());
            cursor = end + 1;
        }

        Assert.NotEmpty(destinations);
        foreach (var destination in destinations)
        {
            if (destination.StartsWith('#') || Uri.TryCreate(destination, UriKind.Absolute, out _))
            {
                continue;
            }

            var fragmentStart = destination.IndexOf('#', StringComparison.Ordinal);
            var filePart = fragmentStart >= 0 ? destination[..fragmentStart] : destination;
            Assert.True(filePart.Length > 0, $"Register link '{destination}' names no file.");
            var resolved = Path.GetFullPath(Path.Combine(registerDirectory, filePart.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase), $"Register link '{destination}' resolves outside the repository.");
            Assert.True(File.Exists(resolved), $"Register link '{destination}' does not resolve to a repository file ({resolved}).");
        }
    }

    [Fact]
    public void The_register_states_its_rules_and_names_its_guard()
    {
        Assert.Contains("sightings, not diagnoses", Register, StringComparison.Ordinal);
        Assert.Contains("A passing rerun is a non-reproduction", Register, StringComparison.Ordinal);
        Assert.Contains("Rows are never deleted", Register, StringComparison.Ordinal);
        Assert.Contains("Raising a timeout is not a fix", Register, StringComparison.Ordinal);
        Assert.Contains("SightingsRegisterTests", Register, StringComparison.Ordinal);
        Assert.Contains("evidence-ledger.json", Register, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Identifiers(string line)
    {
        foreach (Match match in TestIdentifier().Matches(line))
        {
            yield return match.Groups["identifier"].Value;
        }

        foreach (Match match in ConsoleSignalToken().Matches(line))
        {
            yield return match.Value;
        }
    }

    private static List<string> SourceFiles(string root)
        => [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    private static string Section(string content, string heading, string nextHeading)
    {
        var start = content.IndexOf(heading, StringComparison.Ordinal);
        var end = content.IndexOf(nextHeading, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not find register section '{heading}'.");
        return content[start..end];
    }

    private static List<RegisterRow> TableRows(string section, string prefix)
    {
        var rows = new List<RegisterRow>();
        foreach (var line in section.Split('\n'))
        {
            var fields = line.TrimEnd('\r').Split('|', StringSplitOptions.TrimEntries);
            if (fields.Length > 2 && fields[1].StartsWith(prefix, StringComparison.Ordinal))
            {
                rows.Add(new RegisterRow(fields[1], fields[2..^1]));
            }
        }

        return rows;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for sightings-register tests.");
    }
}
