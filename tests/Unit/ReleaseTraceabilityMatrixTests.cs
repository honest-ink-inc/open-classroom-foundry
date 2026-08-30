// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Tests.Unit;

public class ReleaseTraceabilityMatrixTests
{
    private sealed record TraceabilityRow(string Id, IReadOnlyList<string> Cells);

    private static readonly string[] ExpectedUniversalDefinitionOfDoneIds =
    [
        "P-01", "P-02", "P-03", "P-04", "P-05", "P-06", "P-07",
        "PS-01", "PS-02", "PS-03", "PS-04", "PS-05", "PS-06", "PS-07",
        "AL-01", "AL-02", "AL-03", "AL-04", "AL-05",
        "RO-01", "RO-02", "RO-03", "RO-04", "RO-05",
        "OP-01", "OP-02", "OP-03", "OP-04", "OP-05", "OP-06",
        "SU-01", "SU-02", "SU-03", "SU-04",
    ];

    private static readonly string[] ExpectedStopShipIds =
    [
        "SS-01", "SS-02", "SS-03", "SS-04", "SS-05", "SS-06", "SS-07",
        "SS-08", "SS-09", "SS-10", "SS-11", "SS-12", "SS-13",
    ];

    [Fact]
    public void Matrix_inventories_every_definition_of_done_and_stop_ship_id_once_in_order()
    {
        var matrix = File.ReadAllText(MatrixPath());
        var plan = File.ReadAllText(ImplementationPlanPath());
        var universalSection = Section(matrix, "## Universal Definition of Done", "## Stop-ship register");
        var stopShipSection = Section(matrix, "## Stop-ship register", "## Release evidence still required");
        var universalRows = TableRows(universalSection, ["P-", "PS-", "AL-", "RO-", "OP-", "SU-"]);
        var stopShipRows = TableRows(stopShipSection, ["SS-"]);
        var universalIds = universalRows.Select(row => row.Id).ToList();
        var stopShipIds = stopShipRows.Select(row => row.Id).ToList();
        var plannedUniversalRequirements = BulletRequirements(Section(
            plan,
            "# 11. Universal Definition of Done",
            "# 12. Evaluation and test program"));
        var plannedStopShipRequirements = BulletRequirements(Section(
            plan,
            "# 19. Stop-ship conditions",
            "# 20. Decisions to make before coding beyond the skeleton"));

        Assert.Equal(34, universalIds.Count);
        Assert.Equal(universalIds.Count, universalIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedUniversalDefinitionOfDoneIds, universalIds);
        Assert.Equal(34, plannedUniversalRequirements.Count);
        Assert.Equal(
            plannedUniversalRequirements,
            universalRows.Select(row => NormalizeRequirement(row.Cells[0])));
        AssertMappedRows(universalRows, expectedCellCount: 4);

        Assert.Equal(13, stopShipIds.Count);
        Assert.Equal(stopShipIds.Count, stopShipIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedStopShipIds, stopShipIds);
        Assert.Equal(13, plannedStopShipRequirements.Count);
        Assert.Equal(
            plannedStopShipRequirements,
            stopShipRows.Select(row => NormalizeRequirement(row.Cells[0])));
        AssertMappedRows(stopShipRows, expectedCellCount: 3);
    }

    [Fact]
    public void Every_relative_markdown_file_link_in_the_matrix_resolves_inside_the_repository()
    {
        var matrixPath = MatrixPath();
        var matrixDirectory = Path.GetDirectoryName(matrixPath)
            ?? throw new InvalidOperationException("The traceability matrix has no parent directory.");
        var repositoryRoot = FindRepositoryRoot();
        var rootPrefix = repositoryRoot + Path.DirectorySeparatorChar;
        var matrix = File.ReadAllText(matrixPath);
        var referenceDefinitions = matrix.Split('\n')
            .Where(IsReferenceLinkDefinition)
            .ToList();
        Assert.Empty(referenceDefinitions);
        Assert.DoesNotContain("][", matrix, StringComparison.Ordinal);

        var destinations = MarkdownLinkDestinations(matrix).ToList();

        Assert.NotEmpty(destinations);
        foreach (var destination in destinations)
        {
            if (destination.StartsWith('#')
                || Uri.TryCreate(destination, UriKind.Absolute, out _))
            {
                continue;
            }

            var filePart = DestinationFilePart(destination);
            if (filePart.Length == 0)
            {
                continue;
            }

            var portablePath = Uri.UnescapeDataString(filePart)
                .Replace('/', Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(matrixDirectory, portablePath));

            Assert.True(
                resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase),
                $"Relative matrix link '{destination}' resolves outside the repository.");
            Assert.True(
                File.Exists(resolved),
                $"Relative matrix link '{destination}' does not resolve to a repository file ({resolved}).");

            var fragment = DestinationFragment(destination);
            if (fragment.Length > 0 && string.Equals(Path.GetExtension(resolved), ".md", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(
                    Uri.UnescapeDataString(fragment),
                    MarkdownHeadingAnchors(resolved),
                    StringComparer.Ordinal);
            }
        }
    }

    private static string MatrixPath()
        => Path.Combine(FindRepositoryRoot(), "docs", "release", "release-requirement-test-traceability.md");

    private static string ImplementationPlanPath()
        => Path.Combine(FindRepositoryRoot(), "docs", "implementation-plan.md");

    private static string Section(string content, string heading, string nextHeading)
    {
        var start = content.IndexOf(heading, StringComparison.Ordinal);
        var end = content.IndexOf(nextHeading, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not find matrix section '{heading}'.");
        return content[start..end];
    }

    private static List<TraceabilityRow> TableRows(string section, IReadOnlyList<string> prefixes)
    {
        var rows = new List<TraceabilityRow>();
        foreach (var line in section.Split('\n'))
        {
            var fields = line.TrimEnd('\r').Split('|', StringSplitOptions.TrimEntries);
            if (fields.Length > 2
                && prefixes.Any(prefix => fields[1].StartsWith(prefix, StringComparison.Ordinal)))
            {
                rows.Add(new TraceabilityRow(fields[1], fields[2..^1]));
            }
        }

        return rows;
    }

    private static List<string> BulletRequirements(string section)
        => [.. section.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => NormalizeRequirement(line[2..]))];

    private static string NormalizeRequirement(string requirement)
        => requirement.Trim().TrimEnd('.');

    private static void AssertMappedRows(IReadOnlyList<TraceabilityRow> rows, int expectedCellCount)
    {
        Assert.All(rows, row =>
        {
            Assert.Equal(expectedCellCount, row.Cells.Count);
            Assert.All(
                row.Cells,
                cell => Assert.False(
                    string.IsNullOrWhiteSpace(cell),
                    $"Traceability row '{row.Id}' contains an empty required cell."));
        });
    }

    private static IEnumerable<string> MarkdownLinkDestinations(string content)
    {
        var cursor = 0;
        while (cursor < content.Length)
        {
            var targetStart = content.IndexOf("](", cursor, StringComparison.Ordinal);
            if (targetStart < 0)
            {
                yield break;
            }

            targetStart += 2;
            var targetEnd = FindBalancedDestinationEnd(content, targetStart);
            Assert.True(targetEnd >= 0, "The traceability matrix contains an unterminated Markdown link.");
            yield return content[targetStart..targetEnd].Trim();
            cursor = targetEnd + 1;
        }
    }

    private static int FindBalancedDestinationEnd(string content, int targetStart)
    {
        var depth = 1;
        for (var index = targetStart; index < content.Length; index++)
        {
            if (content[index] == '\\')
            {
                index++;
                continue;
            }

            if (content[index] == '(')
            {
                depth++;
            }
            else if (content[index] == ')' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsReferenceLinkDefinition(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('[')
            && trimmed.IndexOf("]:", StringComparison.Ordinal) > 1;
    }

    private static string DestinationFilePart(string destination)
    {
        var value = DestinationValue(destination);
        var fragment = value.IndexOf('#');
        var query = value.IndexOf('?');
        var suffix = fragment < 0 ? query : query < 0 ? fragment : Math.Min(fragment, query);
        return suffix < 0 ? value : value[..suffix];
    }

    private static string DestinationFragment(string destination)
    {
        var value = DestinationValue(destination);
        var fragment = value.IndexOf('#');
        if (fragment < 0)
        {
            return string.Empty;
        }

        var query = value.IndexOf('?', fragment);
        return query < 0 ? value[(fragment + 1)..] : value[(fragment + 1)..query];
    }

    private static string DestinationValue(string destination)
    {
        var value = destination;
        if (value.StartsWith('<'))
        {
            var closingBracket = value.IndexOf('>');
            Assert.True(closingBracket > 0, $"Markdown link destination '{destination}' has no closing angle bracket.");
            value = value[1..closingBracket];
        }
        else
        {
            var whitespace = value.IndexOfAny([' ', '\t']);
            if (whitespace >= 0)
            {
                value = value[..whitespace];
            }
        }

        return value;
    }

    private static HashSet<string> MarkdownHeadingAnchors(string path)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.TrimStart();
            var markerCount = 0;
            while (markerCount < line.Length && markerCount < 6 && line[markerCount] == '#')
            {
                markerCount++;
            }

            if (markerCount == 0 || markerCount >= line.Length || line[markerCount] != ' ')
            {
                continue;
            }

            var heading = line[(markerCount + 1)..].Trim().TrimEnd('#').Trim();
            var slug = string.Concat(heading.ToLowerInvariant().Where(
                character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '-'))
                .Replace(' ', '-');
            var occurrence = occurrences.GetValueOrDefault(slug);
            occurrences[slug] = occurrence + 1;
            anchors.Add(occurrence == 0 ? slug : $"{slug}-{occurrence}");
        }

        return anchors;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for release-traceability tests.");
    }
}
