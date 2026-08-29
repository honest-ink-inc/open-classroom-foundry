using System.Text;
using Foundry.Tools.SiteGenerator;

namespace Foundry.Tests.Unit;

public class MarkdownLiteTests
{
    [Fact]
    public void Headings_paragraphs_and_emphasis_render_as_semantic_html()
    {
        var html = MarkdownLite.ToHtml("# Title\n\nA **bold** and *quiet* and ~~struck~~ line with `code`.\n");

        Assert.Contains("<h1>Title</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<p>A <strong>bold</strong> and <em>quiet</em> and <del>struck</del> line with <code>code</code>.</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Tables_lists_blockquotes_rules_and_fences_all_render()
    {
        var html = MarkdownLite.ToHtml(
            "| A | B |\n|---|---|\n| 1 | 2 |\n\n- first\n- second\n\n1. one\n2. two\n\n> quoted\n\n---\n\n```\nraw <text>\n```\n");

        Assert.Contains("<thead><tr><th>A</th><th>B</th></tr></thead>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><td>1</td><td>2</td></tr>", html, StringComparison.Ordinal);
        Assert.Contains("<ul>\n<li>first</li>\n<li>second</li>\n</ul>", html, StringComparison.Ordinal);
        Assert.Contains("<ol>\n<li>one</li>\n<li>two</li>\n</ol>", html, StringComparison.Ordinal);
        Assert.Contains("<blockquote><p>quoted</p></blockquote>", html, StringComparison.Ordinal);
        Assert.Contains("<hr>", html, StringComparison.Ordinal);
        Assert.Contains("<pre><code>raw &lt;text&gt;\n</code></pre>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_text_can_never_smuggle_markup()
    {
        var html = MarkdownLite.ToHtml("A <script>alert(1)</script> \"quote\" & ampersand.\n");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&quot;quote&quot;", html, StringComparison.Ordinal);
        Assert.Contains("&amp; ampersand", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Links_pass_through_the_rewriter_and_bold_survives_inside_them()
    {
        var html = MarkdownLite.ToHtml("See [**the plan**](docs/plan.md) now.\n", href => href.Replace(".md", ".html", StringComparison.Ordinal));

        Assert.Contains("<a href=\"docs/plan.html\"><strong>the plan</strong></a>", html, StringComparison.Ordinal);
    }
}

public class SiteBuilderTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void The_site_builds_every_page_deterministically_from_the_real_documents()
    {
        var first = SiteBuilder.Build(RepoRoot());
        var second = SiteBuilder.Build(RepoRoot());

        Assert.Equal(SiteBuilder.Pages.Count, first.Count);
        Assert.Equal(
            first.Select(f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(f.Content))),
            second.Select(f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(f.Content))));
    }

    [Fact]
    public void The_index_carries_the_repository_readme_with_working_navigation()
    {
        var index = Page("index.html");

        Assert.Contains("<h1>Open Classroom Foundry</h1>", index, StringComparison.Ordinal);
        Assert.Contains("<title>Open Classroom Foundry</title>", index, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", index, StringComparison.Ordinal);
        foreach (var page in SiteBuilder.Pages)
        {
            Assert.Contains($"href=\"{page.Slug}.html\"", index, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Links_between_rendered_pages_become_page_links_and_the_rest_point_at_the_repository()
    {
        var index = Page("index.html");

        // README links CONTRIBUTING.md and GOVERNANCE.md — both are pages here.
        Assert.Contains("href=\"contributing.html\"", index, StringComparison.Ordinal);
        Assert.Contains("href=\"governance.html\"", index, StringComparison.Ordinal);

        // The implementation plan is not rendered: an honest repository pointer.
        Assert.Contains($"href=\"{SiteBuilder.RepositoryBase}docs/implementation-plan.md\"", index, StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_links_from_nested_documents_resolve_repo_relative()
    {
        var spec = Page("deterministic-press.html");

        // The spec's own title renders, and no un-rewritten .md hrefs remain.
        Assert.Contains("Deterministic Press", spec, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"../", spec, StringComparison.Ordinal);
    }

    private static string Page(string fileName)
        => Encoding.UTF8.GetString(SiteBuilder.Build(RepoRoot()).Single(f => f.FileName == fileName).Content);
}
