using System.Text;

namespace Foundry.Tests.Unit;

/// <summary>
/// The localization architecture rule (handover 2026-08-29, forge item 4):
/// every user-facing chrome string lives in the UiStrings catalog, nowhere
/// else. Enforced as a source scan so a hard-coded literal fails the suite the
/// moment it is written. Heuristic: a quoted literal containing a space is
/// user-facing prose; identifiers, file extensions, mime types, and switch
/// names are space-free and pass. Exact machine-only IPC/HTTP grammar is
/// separately inventoried below; comments are stripped before scanning.
/// </summary>
public class LocalizationRulesTests
{
    private static readonly HashSet<(string FileName, string Literal)> MachineProtocolLiterals =
    [
        ("AppServices.PrintViewHandoff.cs", "READY "),
        ("AppServices.PrintViewHandoff.cs", "READY {url}"),
        ("AppServices.PrintViewHandoff.cs", "GET /{token}/ HTTP/1.1"),
        ("AppServices.PrintViewHandoff.cs", "HTTP/1.1 200 OK\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Content-Type: text/html; charset=utf-8\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Content-Length: {content.Length.ToString(CultureInfo.InvariantCulture)}\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Cache-Control: no-store\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Pragma: no-cache\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "X-Content-Type-Options: nosniff\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Content-Security-Policy: default-src 'none'; img-src data:; style-src 'unsafe-inline'\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "Connection: close\\r\\n\\r\\n"),
        ("AppServices.PrintViewHandoff.cs", "HTTP/1.1 404 Not Found\\r\\nContent-Length: 0\\r\\nConnection: close\\r\\n\\r\\n"),
    ];

    [Fact]
    public void The_winforms_app_carries_no_user_facing_string_outside_the_catalog()
    {
        var appRoot = Path.Combine(RepoRoot(), "src", "Foundry.App.WinForms");
        var catalog = Path.Combine(appRoot, "Localization", "UiStrings.cs");
        Assert.True(File.Exists(catalog), "The UiStrings catalog must exist.");

        var offenders = new List<string>();
        var observedMachineProtocolLiterals = new HashSet<(string FileName, string Literal)>();
        var files = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !string.Equals(f, catalog, StringComparison.OrdinalIgnoreCase)
                // UiaHarness carries fixture DOCUMENT content (a sample strip's
                // steps) — artifact content, not chrome; content localization
                // belongs to the document's own Language contract.
                && !string.Equals(Path.GetFileName(f), "UiaHarness.cs", StringComparison.OrdinalIgnoreCase)
                // ProductIdentity is ADR-006's single ship-name record: the
                // public name never localizes, and the subtitle it holds is
                // the neutral source that UiStrings composes and localizes.
                && !string.Equals(Path.GetFileName(f), "ProductIdentity.cs", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lineNumber = 0;
            foreach (var raw in File.ReadLines(file))
            {
                lineNumber++;
                foreach (var literal in StringLiterals(StripComment(raw)))
                {
                    if (literal.Contains(' ', StringComparison.Ordinal))
                    {
                        var protocolLiteral = (fileName, literal);
                        if (MachineProtocolLiterals.Contains(protocolLiteral))
                        {
                            observedMachineProtocolLiterals.Add(protocolLiteral);
                            continue;
                        }

                        offenders.Add($"{fileName}:{lineNumber} \"{literal}\"");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "User-facing strings belong in UiStrings, not inline:\n" + string.Join('\n', offenders));
        var staleProtocolLiterals = MachineProtocolLiterals
            .Except(observedMachineProtocolLiterals)
            .Select(item => $"{item.FileName} \"{item.Literal}\"")
            .ToArray();
        Assert.True(staleProtocolLiterals.Length == 0,
            "Machine-protocol literal inventory no longer matches the source:\n"
            + string.Join('\n', staleProtocolLiterals));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (OpenClassroomFoundry.slnx) not found.");
    }

    /// <summary>Cuts a trailing // comment, honoring quotes so "https://" survives inside literals.</summary>
    private static string StripComment(string line)
    {
        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static IEnumerable<string> StringLiterals(string line)
    {
        var current = new StringBuilder();
        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                if (inQuote)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                inQuote = !inQuote;
            }
            else if (inQuote)
            {
                current.Append(line[i]);
            }
        }
    }
}
