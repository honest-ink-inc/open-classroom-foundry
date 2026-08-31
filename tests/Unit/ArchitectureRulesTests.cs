using System.Xml.Linq;

namespace Foundry.Tests.Unit;

/// <summary>
/// Executable form of the boundary rules in src/README.md, ADR-001, and the
/// Deterministic Press specification. These tests read the .csproj files
/// directly, so a forbidden ProjectReference fails the suite even before any
/// code exists to violate it at runtime.
/// </summary>
public class ArchitectureRulesTests
{
    private static readonly string[] DirectPlatformReachTokens =
    [
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.FileStream",
        "System.IO.DriveInfo",
        "System.IO.FileSystemWatcher",
        "System.Net.Http.HttpClient",
        "System.Net.Sockets",
        "System.Diagnostics.Process",
        "System.Drawing.Printing",
        "Microsoft.Win32",
        "Windows.Devices",
        "File.",
        "Directory.",
        "FileStream(",
        "FileInfo(",
        "DirectoryInfo(",
        "DriveInfo(",
        "FileSystemWatcher(",
        "Path.",
        "HttpClient(",
        "WebRequest.",
        "NetworkStream(",
        "Socket(",
        "TcpClient(",
        "TcpListener(",
        "UdpClient(",
        "Process.",
        "ProcessStartInfo(",
        "PrintDocument(",
        "PrinterSettings(",
        "Environment.GetFolderPath",
    ];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (OpenClassroomFoundry.slnx) not found.");
    }

    private static List<string> ProjectReferences(string relativeCsprojPath)
    {
        var path = Path.Combine(RepoRoot(), relativeCsprojPath);
        var document = XDocument.Load(path);
        return [.. document.Descendants("ProjectReference").Select(r => (string?)r.Attribute("Include") ?? string.Empty)];
    }

    [Theory]
    [InlineData(@"src\Foundry.Modules.DeterministicPress\Foundry.Modules.DeterministicPress.csproj")]
    [InlineData(@"src\Foundry.Modules.BuiltIn\Foundry.Modules.BuiltIn.csproj")]
    public void Modules_may_not_reference_inference_or_infrastructure(string csproj)
    {
        var references = ProjectReferences(csproj);

        Assert.DoesNotContain(references, r => r.Contains("Inference", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("App.WinForms", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Modules_do_not_use_known_direct_platform_api_vocabulary()
    {
        // ProjectReference checks close the architectural door to adapters;
        // this bounded source vocabulary closes the BCL side doors that do not
        // require a project reference. It is deliberately narrower than a
        // claim that all System.IO is impure: in-memory streams are valid.
        var root = RepoRoot();
        var moduleRoots = new[]
        {
            Path.Combine(root, "src", "Foundry.Modules.DeterministicPress"),
            Path.Combine(root, "src", "Foundry.Modules.BuiltIn"),
        };
        var offenders = new List<string>();
        foreach (var file in moduleRoots
                     .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
                     .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                             || segment.Equals("obj", StringComparison.OrdinalIgnoreCase))))
        {
            var source = File.ReadAllText(file);
            foreach (var token in DirectPlatformReachTokens.Where(token => source.Contains(token, StringComparison.Ordinal)))
            {
                offenders.Add($"{Path.GetRelativePath(root, file)} ({token})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "ADR-001 requires modules to use engine service seams rather than direct platform reach:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void Every_source_file_carries_its_spdx_license_identifier()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tools"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in files)
        {
            Assert.Contains("SPDX-License-Identifier: GPL-3.0-or-later", File.ReadLines(file).First(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Domain_references_no_projects()
    {
        Assert.Empty(ProjectReferences(@"src\Foundry.Domain\Foundry.Domain.csproj"));
    }

    [Fact]
    public void Contracts_references_only_domain()
    {
        var references = ProjectReferences(@"src\Foundry.Contracts\Foundry.Contracts.csproj");

        var reference = Assert.Single(references);
        Assert.Contains("Foundry.Domain", reference, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"src\Foundry.Domain\Foundry.Domain.csproj")]
    [InlineData(@"src\Foundry.Contracts\Foundry.Contracts.csproj")]
    [InlineData(@"src\Foundry.Application\Foundry.Application.csproj")]
    public void Engine_core_may_not_reference_ui_or_windows_infrastructure(string csproj)
    {
        var references = ProjectReferences(csproj);

        Assert.DoesNotContain(references, r => r.Contains("App.WinForms", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Infrastructure.Windows", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Simulated_infrastructure_references_only_domain_and_contracts()
    {
        var references = ProjectReferences(@"src\Foundry.Infrastructure.Simulated\Foundry.Infrastructure.Simulated.csproj");

        Assert.Equal(2, references.Count);
        Assert.Contains(references, r => r.Contains("Foundry.Domain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(references, r => r.Contains("Foundry.Contracts", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"src\Foundry.Inference.AzureOpenAI\Foundry.Inference.AzureOpenAI.csproj")]
    [InlineData(@"src\Foundry.Inference.Local\Foundry.Inference.Local.csproj")]
    [InlineData(@"src\Foundry.Inference.Synthetic\Foundry.Inference.Synthetic.csproj")]
    public void Inference_adapters_reference_only_the_abstractions(string csproj)
    {
        var reference = Assert.Single(ProjectReferences(csproj));
        Assert.Contains("Foundry.Inference.Abstractions", reference, StringComparison.OrdinalIgnoreCase);
    }

    // D17 / D19 Q5: the outbound surface is the claim a district security reviewer
    // is most likely to lean on, and until now nothing in this repository checked
    // it -- the review instrument that did lives outside the build and its pattern
    // could not see a listening socket.
    //
    // This is deliberately an ALLOWLIST WITH REASONS rather than a count. "At most
    // one file" invites the question "which one, and how do you know?", and it is
    // not the rule this project actually holds: one file reaches outward, and one
    // serves a bounded loopback handoff. A count would also have to be edited into
    // a lie the day a second inference provider ships. Adding a network-capable
    // file should be a decision, and this test is where that decision is recorded.
    private static readonly string[] NetworkReachTokens =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "HttpClient",
        "HttpRequestMessage",
        "HttpListener",
        "WebRequest",
        "TcpListener",
        "TcpClient",
        "UdpClient",
        "NetworkStream",
        "Socket(",
        "Dns.",
    ];

    // Bare `System.Net` is deliberately absent: WebUtility.HtmlEncode lives there,
    // and two renderers import the namespace for escaping alone. Treating that as
    // network reach would make this test cry wolf, and an ignored checker is worse
    // than none.
    private static readonly Dictionary<string, string> NetworkCapableFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Foundry.Inference.AzureOpenAI/AzureOpenAIProvider.cs"] =
                "The only outbound path. Council finding R2-12 makes an endpoint off the "
                + "district allowlist unconstructable; redirects are disabled and any 3xx "
                + "is refused rather than followed.",
            ["src/Foundry.App.WinForms/AppServices.PrintViewHandoff.cs"] =
                "Inbound only, and never off-machine: a one-shot TcpListener bound to "
                + "IPAddress.Loopback on an ephemeral port, in a short-lived child process, "
                + "gated by a single-use path token and an absolute deadline.",
        };

    [Fact]
    public void Only_declared_files_may_touch_the_network()
    {
        var root = RepoRoot();
        var offenders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory
                     .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                             || segment.Equals("obj", StringComparison.OrdinalIgnoreCase))))
        {
            var source = File.ReadAllText(file);
            var matched = NetworkReachTokens
                .Where(token => source.Contains(token, StringComparison.Ordinal))
                .ToArray();
            if (matched.Length == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (NetworkCapableFiles.ContainsKey(relative))
            {
                seen.Add(relative);
                continue;
            }

            offenders.Add($"{relative} ({string.Join(", ", matched)})");
        }

        Assert.True(
            offenders.Count == 0,
            "A file outside the declared network allowlist reaches the network. If this is "
            + "intended, add it to NetworkCapableFiles with the reason it is safe:\n"
            + string.Join('\n', offenders));

        // An allowlist that keeps entries it no longer needs stops describing the
        // tree and starts excusing it.
        var stale = NetworkCapableFiles.Keys.Where(declared => !seen.Contains(declared)).ToArray();
        Assert.True(
            stale.Length == 0,
            "The network allowlist names files that no longer reach the network; remove them:\n"
            + string.Join('\n', stale));
    }
}
