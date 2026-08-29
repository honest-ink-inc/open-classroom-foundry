// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace Foundry.Tools.SiteGenerator;

public sealed record SitePage(string Slug, string SourcePath, string NavLabel);

/// <summary>
/// Renders the repository's governing documents to a deterministic static site
/// for the purchased domain. The documents ARE the content — no copy is written
/// here, no timestamp is stamped, and identical inputs produce byte-identical
/// pages. Publishing — hosting choice, DNS, upload — is the typist's act; this
/// forge only makes the artifact (handover 2026-08-29, forge item 7).
/// </summary>
public static class SiteBuilder
{
    /// <summary>Links to repository files outside the rendered set resolve here.</summary>
    public const string RepositoryBase = "https://github.com/Spacejunk-io/open-classroom-foundry/blob/main/";

    public static IReadOnlyList<SitePage> Pages { get; } =
    [
        new SitePage("index", "README.md", "Home"),
        new SitePage("governance", "GOVERNANCE.md", "Governance"),
        new SitePage("contributing", "CONTRIBUTING.md", "Contributing"),
        new SitePage("security", "SECURITY.md", "Security"),
        new SitePage("notice", "NOTICE.md", "Notices"),
        new SitePage("deterministic-press", "docs/modules/deterministic-press-spec.md", "Deterministic Press"),
    ];

    public static IReadOnlyList<(string FileName, byte[] Content)> Build(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var slugBySource = Pages.ToDictionary(p => p.SourcePath, p => p.Slug, StringComparer.OrdinalIgnoreCase);
        var files = new List<(string, byte[])>();

        foreach (var page in Pages)
        {
            var markdown = File.ReadAllText(Path.Combine(repoRoot, page.SourcePath));
            var sourceDirectory = Path.GetDirectoryName(page.SourcePath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;

            var body = MarkdownLite.ToHtml(markdown, href => Rewrite(href, sourceDirectory, slugBySource));
            var title = FirstHeading(markdown) ?? page.NavLabel;

            files.Add(($"{page.Slug}.html", Encoding.UTF8.GetBytes(Shell(title, page.Slug, body))));
        }

        // The samples gallery (menu 4, item 8): engine-rendered, not
        // markdown-backed — the presses drawing their own portraits.
        files.Add(($"{SampleGallery.Slug}.html",
            Encoding.UTF8.GetBytes(Shell("Press samples", SampleGallery.Slug, SampleGallery.BodyHtml()))));

        return files;
    }

    /// <summary>
    /// Relative .md links inside the rendered set become page links; every other
    /// relative repository link resolves to the repository itself — an honest
    /// pointer beats a broken one. Absolute links and anchors pass through.
    /// </summary>
    private static string Rewrite(string href, string sourceDirectory, Dictionary<string, string> slugBySource)
    {
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith('#')
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        var anchorIndex = href.IndexOf('#', StringComparison.Ordinal);
        var anchor = anchorIndex >= 0 ? href[anchorIndex..] : string.Empty;
        var path = anchorIndex >= 0 ? href[..anchorIndex] : href;

        var normalized = Normalize(sourceDirectory.Replace(Path.DirectorySeparatorChar, '/'), path);

        return slugBySource.TryGetValue(normalized, out var slug)
            ? $"{slug}.html{anchor}"
            : RepositoryBase + normalized + anchor;
    }

    /// <summary>Repo-relative path with "." and ".." folded away — no filesystem involved, so the result is platform-free.</summary>
    private static string Normalize(string sourceDirectory, string path)
    {
        var segments = new List<string>();
        foreach (var segment in $"{sourceDirectory}/{path}".Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static string? FirstHeading(string markdown)
        => markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();

    private static string Shell(string title, string activeSlug, string body)
    {
        var entries = Pages.Select(p => (p.Slug, p.NavLabel))
            .Append((SampleGallery.Slug, SampleGallery.NavLabel));
        var nav = string.Join("\n", entries.Select(p => p.Slug == activeSlug
            ? $"<a href=\"{p.Slug}.html\" aria-current=\"page\">{p.NavLabel}</a>"
            : $"<a href=\"{p.Slug}.html\">{p.NavLabel}</a>"));

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{title}}</title>
<style>
:root { color-scheme: light; }
body { margin: 0; font-family: Georgia, 'Times New Roman', serif; color: #1a1a1a; background: #faf9f6; line-height: 1.6; }
header { border-bottom: 2px solid #1a1a1a; padding: 0.75rem 1rem; }
nav { display: flex; flex-wrap: wrap; gap: 1rem; max-width: 46rem; margin: 0 auto; }
nav a { color: #1a1a1a; text-decoration: none; font-variant: small-caps; letter-spacing: 0.04em; }
nav a[aria-current] { border-bottom: 2px solid #1a1a1a; }
main { max-width: 46rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; }
h1, h2, h3 { line-height: 1.25; }
blockquote { margin: 1rem 0; padding: 0.25rem 1rem; border-left: 3px solid #1a1a1a; font-style: italic; }
table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
th, td { border: 1px solid #999; padding: 0.4rem 0.6rem; text-align: left; vertical-align: top; }
code { font-family: Consolas, 'Courier New', monospace; background: #efede8; padding: 0 0.2rem; }
pre { background: #efede8; padding: 0.75rem; overflow-x: auto; }
a { color: #14507a; }
footer { max-width: 46rem; margin: 0 auto; padding: 1rem; border-top: 1px solid #999; font-size: 0.85rem; }
figure.sample { margin: 2rem 0; }
figure.sample svg { width: 100%; height: auto; border: 1px solid #999; background: #fff; }
figure.sample figcaption { margin-top: 0.5rem; font-size: 0.9rem; }
@media print { header, footer { display: none; } body { background: #fff; } }
</style>
</head>
<body>
<header>
<nav>
{{nav}}
</nav>
</header>
<main>
{{body}}</main>
<footer>
<p>Code licensed GPL-3.0-or-later. This page is generated deterministically from the repository's governing documents; the <a href="{{RepositoryBase}}">repository</a> is canonical.</p>
</footer>
</body>
</html>
""";
    }
}
