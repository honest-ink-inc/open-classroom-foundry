// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace Foundry.Tests.Unit;

public sealed class WorkflowActionRuntimeContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Workflow_actions_match_the_complete_reviewed_runtime_inventory()
    {
        var inventoryPath = Path.Combine(
            Root,
            "docs",
            "release",
            "workflow-action-runtime-inventory.json");
        Assert.True(
            File.Exists(inventoryPath),
            $"The reviewed workflow-action runtime inventory is missing: {inventoryPath}");

        var inventoryJson = File.ReadAllText(inventoryPath);
        using var inventoryDocument = JsonDocument.Parse(inventoryJson);
        var inventoryRoot = inventoryDocument.RootElement;
        Assert.Equal("2026-08-31", inventoryRoot.GetProperty("reviewedOn").GetString());

        var directReviews = inventoryRoot.GetProperty("directActions")
            .EnumerateArray()
            .Select(ReadDirectReview)
            .ToArray();
        var transitiveReviews = inventoryRoot.GetProperty("transitiveActions")
            .EnumerateArray()
            .Select(ReadTransitiveReview)
            .ToArray();

        Assert.DoesNotContain("node20", inventoryJson, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            directReviews.Where(review => review.Runtime != "composite"),
            review => Assert.Equal("node24", review.Runtime));
        Assert.All(transitiveReviews, review => Assert.Equal("node24", review.Runtime));

        var errors = Validate(
            ReadWorkflowUses(),
            directReviews,
            transitiveReviews);

        Assert.True(
            errors.Count == 0,
            "Workflow actions must match the complete offline-reviewed runtime inventory:\n"
            + string.Join('\n', errors));
    }

    [Fact]
    public void Reviewed_inventory_validator_rejects_an_immutable_but_node20_generation()
    {
        var reviewed = new[]
        {
            new DirectReview(
                "actions/checkout",
                "3d3c42e5aac5ba805825da76410c181273ba90b1",
                "v7.0.1",
                "node24",
                "https://github.com/actions/checkout/blob/3d3c42e5aac5ba805825da76410c181273ba90b1/action.yml"),
        };
        var substituted = new[]
        {
            new WorkflowUse(
                ".github/workflows/adversarial.yml:1",
                "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
                "v4.2.2"),
        };

        var errors = Validate(substituted, reviewed, []);

        Assert.Contains(errors, error => error.Contains("reviewed commit", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("reviewed release", StringComparison.Ordinal));
    }

    [Fact]
    public void Reviewed_inventory_validator_rejects_an_unreviewed_local_composite()
    {
        var errors = Validate(
            [new WorkflowUse(".github/workflows/adversarial.yml:1", "./.github/actions/local", string.Empty)],
            [],
            []);

        Assert.Contains(errors, error => error.Contains("unreviewed local action", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("  - uses : actions/unreviewed@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # v1.0.0", "unknown action selector")]
    [InlineData("  - 'uses' : ./.github/actions/unreviewed", "unreviewed local action")]
    [InlineData("  - \"uses\" : actions/unreviewed@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa # v1.0.0", "unknown action selector")]
    public void Workflow_scanner_recognizes_supported_uses_key_spellings_and_rejects_unreviewed_actions(
        string declaration,
        string expectedError)
    {
        var uses = ReadWorkflowUsesFromLines(
            [declaration],
            ".github/workflows/adversarial.yml");

        var parsed = Assert.Single(uses);
        Assert.Equal(".github/workflows/adversarial.yml:1", parsed.Source);
        var errors = Validate(uses, [], []);
        Assert.Contains(errors, error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("  - { uses: actions/unreviewed@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa }")]
    [InlineData("  ? uses")]
    public void Workflow_scanner_fails_closed_on_unsupported_uses_mapping_shapes(string declaration)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadWorkflowUsesFromLines(
            [declaration],
            ".github/workflows/adversarial.yml"));

        Assert.Contains("cannot safely inventory", exception.Message, StringComparison.Ordinal);
    }

    private static List<string> Validate(
        IReadOnlyCollection<WorkflowUse> uses,
        IReadOnlyCollection<DirectReview> directReviews,
        IReadOnlyCollection<TransitiveReview> transitiveReviews)
    {
        var errors = new List<string>();
        var duplicateReviews = directReviews
            .GroupBy(review => review.Selector, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateReviews.Length > 0)
        {
            errors.Add("duplicate direct inventory selectors: " + string.Join(", ", duplicateReviews));
        }

        var reviewedBySelector = directReviews
            .GroupBy(review => review.Selector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seenSelectors = new HashSet<string>(StringComparer.Ordinal);
        var parsedUses = new List<ParsedWorkflowUse>();

        foreach (var use in uses)
        {
            if (use.Reference.StartsWith("./", StringComparison.Ordinal))
            {
                errors.Add($"{use.Source} uses an unreviewed local action: {use.Reference}");
                continue;
            }

            var at = use.Reference.LastIndexOf('@');
            if (at <= 0)
            {
                errors.Add($"{use.Source} has a malformed action reference: {use.Reference}");
                continue;
            }

            var selector = use.Reference[..at];
            var commit = use.Reference[(at + 1)..];
            parsedUses.Add(new ParsedWorkflowUse(use.Source, selector, commit, use.Release));
            seenSelectors.Add(selector);

            if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
            {
                errors.Add($"{use.Source} does not use a 40-hex commit: {use.Reference}");
            }

            if (!reviewedBySelector.TryGetValue(selector, out var review))
            {
                errors.Add($"{use.Source} uses an unknown action selector: {selector}");
                continue;
            }

            if (!string.Equals(commit, review.Commit, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{use.Source} commit {commit} differs from reviewed commit {review.Commit} for {selector}");
            }

            if (!string.Equals(use.Release, review.Release, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{use.Source} comment '{use.Release}' differs from reviewed release '{review.Release}' for {selector}");
            }

            ValidateImmutableMetadataUrl(review.Selector, review.Commit, review.MetadataUrl, errors);
        }

        var divergentSelectors = parsedUses
            .GroupBy(use => use.Selector, StringComparer.Ordinal)
            .Where(group => group
                .Select(use => (use.Commit, use.Release))
                .Distinct()
                .Skip(1)
                .Any())
            .Select(group => group.Key)
            .ToArray();
        if (divergentSelectors.Length > 0)
        {
            errors.Add("workflow occurrences diverge for: " + string.Join(", ", divergentSelectors));
        }

        foreach (var missing in reviewedBySelector.Keys.Except(seenSelectors, StringComparer.Ordinal))
        {
            errors.Add($"reviewed action selector is absent from workflows: {missing}");
        }

        var compositeSelectors = directReviews
            .Where(review => review.Runtime == "composite")
            .Select(review => review.Selector)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var review in directReviews)
        {
            if (review.Runtime is not ("node24" or "composite"))
            {
                errors.Add($"{review.Selector} has an unsupported reviewed runtime: {review.Runtime}");
            }
        }

        foreach (var transitive in transitiveReviews)
        {
            if (!compositeSelectors.Contains(transitive.ViaSelector))
            {
                errors.Add($"transitive action has no reviewed composite parent: {transitive.ViaSelector}");
            }

            if (transitive.Runtime != "node24")
            {
                errors.Add($"{transitive.Selector} has a non-Node-24 transitive runtime: {transitive.Runtime}");
            }

            if (transitive.Commit.Length != 40 || !transitive.Commit.All(Uri.IsHexDigit))
            {
                errors.Add($"{transitive.Selector} does not use a reviewed 40-hex transitive commit");
            }

            ValidateImmutableMetadataUrl(
                transitive.Selector,
                transitive.Commit,
                transitive.MetadataUrl,
                errors);
        }

        foreach (var composite in compositeSelectors)
        {
            if (!transitiveReviews.Any(review => review.ViaSelector == composite))
            {
                errors.Add($"reviewed composite has no reviewed transitive action: {composite}");
            }
        }

        return errors;
    }

    private static void ValidateImmutableMetadataUrl(
        string selector,
        string commit,
        string metadataUrl,
        List<string> errors)
    {
        var repository = string.Join('/', selector.Split('/').Take(2));
        var immutablePrefix = $"https://github.com/{repository}/blob/{commit}/";
        if (!metadataUrl.StartsWith(immutablePrefix, StringComparison.Ordinal)
            || !(metadataUrl.EndsWith("action.yml", StringComparison.Ordinal)
                || metadataUrl.EndsWith("action.yaml", StringComparison.Ordinal)))
        {
            errors.Add($"{selector} lacks an immutable reviewed action metadata URL");
        }
    }

    private static List<WorkflowUse> ReadWorkflowUses()
    {
        var workflowRoot = Path.Combine(Root, ".github", "workflows");
        var uses = new List<WorkflowUse>();
        foreach (var workflowPath in Directory.EnumerateFiles(workflowRoot, "*", SearchOption.AllDirectories)
                      .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                          || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = Path.GetRelativePath(Root, workflowPath).Replace('\\', '/');
            uses.AddRange(ReadWorkflowUsesFromLines(File.ReadLines(workflowPath), relativePath));
        }

        return uses;
    }

    private static List<WorkflowUse> ReadWorkflowUsesFromLines(
        IEnumerable<string> lines,
        string workflowPath)
    {
        var uses = new List<WorkflowUse>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            var declaration = line.TrimStart();
            if (declaration.Length > 1
                && declaration[0] == '-'
                && char.IsWhiteSpace(declaration[1]))
            {
                declaration = declaration[1..].TrimStart();
            }

            if (!TryReadBlockUsesValue(declaration, out var value))
            {
                if (ContainsUnsupportedUsesMapping(declaration))
                {
                    throw new InvalidDataException(
                        $"{workflowPath}:{lineNumber} contains a uses mapping shape that the "
                        + "runtime inventory cannot safely inventory. Use one block mapping per line.");
                }

                continue;
            }

            var commentAt = value.IndexOf('#');
            var reference = (commentAt >= 0 ? value[..commentAt] : value).Trim().Trim('\'', '"');
            var release = commentAt >= 0 ? value[(commentAt + 1)..].Trim() : string.Empty;
            uses.Add(new WorkflowUse($"{workflowPath}:{lineNumber}", reference, release));
        }

        return uses;
    }

    private static bool TryReadBlockUsesValue(string declaration, out string value)
    {
        foreach (var key in new[] { "uses", "'uses'", "\"uses\"" })
        {
            if (!declaration.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            var separator = key.Length;
            while (separator < declaration.Length && char.IsWhiteSpace(declaration[separator]))
            {
                separator++;
            }

            if (separator < declaration.Length && declaration[separator] == ':')
            {
                value = declaration[(separator + 1)..].Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsUnsupportedUsesMapping(string declaration)
    {
        foreach (var mappingSeparator in new[] { '{', ',' })
        {
            var searchFrom = 0;
            while (searchFrom < declaration.Length)
            {
                var separator = declaration.IndexOf(mappingSeparator, searchFrom);
                if (separator < 0)
                {
                    break;
                }

                if (TryReadBlockUsesValue(declaration[(separator + 1)..].TrimStart(), out _))
                {
                    return true;
                }

                searchFrom = separator + 1;
            }
        }

        if (declaration.Length == 0 || declaration[0] != '?')
        {
            return false;
        }

        var explicitKey = declaration[1..].Trim();
        return explicitKey is "uses" or "'uses'" or "\"uses\"";
    }

    private static DirectReview ReadDirectReview(JsonElement element)
        => new(
            RequiredString(element, "selector"),
            RequiredString(element, "commit"),
            RequiredString(element, "release"),
            RequiredString(element, "runtime"),
            RequiredString(element, "metadataUrl"));

    private static TransitiveReview ReadTransitiveReview(JsonElement element)
        => new(
            RequiredString(element, "viaSelector"),
            RequiredString(element, "selector"),
            RequiredString(element, "commit"),
            RequiredString(element, "release"),
            RequiredString(element, "runtime"),
            RequiredString(element, "metadataUrl"));

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Inventory property '{propertyName}' must be non-blank.")
            : value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root for workflow-action tests.");
    }

    private sealed record WorkflowUse(string Source, string Reference, string Release);

    private sealed record ParsedWorkflowUse(string Source, string Selector, string Commit, string Release);

    private sealed record DirectReview(
        string Selector,
        string Commit,
        string Release,
        string Runtime,
        string MetadataUrl);

    private sealed record TransitiveReview(
        string ViaSelector,
        string Selector,
        string Commit,
        string Release,
        string Runtime,
        string MetadataUrl);
}
