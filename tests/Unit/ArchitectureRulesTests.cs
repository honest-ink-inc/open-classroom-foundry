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

    // D19 Q4: the same scan, widened from the two module projects to the whole
    // portable engine. The check was never module-specific in principle -- the
    // rule is that platform reach belongs behind a service seam -- and running it
    // over two of eight portable projects left six unscanned for no reason beyond
    // where it was first written. Renamed because the scope changed, not the rule.
    //
    // Verified before widening: zero files in any of these eight trip the token
    // list, while the two platform-coupled projects trip it heavily. The tokens
    // below name specific sub-namespaces on purpose; a bare `System.IO` would flag
    // `System.IO.Compression` over a MemoryStream, which is in-memory work and not
    // platform reach. AccessibleHtmlRenderer uses exactly that today.
    private static readonly string[] PortableEngineProjects =
    [
        "Foundry.Domain",
        "Foundry.Contracts",
        "Foundry.Application",
        "Foundry.Inference.Abstractions",
        "Foundry.Infrastructure.Simulated",
        "Foundry.Modules.BuiltIn",
        "Foundry.Modules.DeterministicPress",
        "Foundry.Rendering",
    ];

    [Fact]
    public void The_portable_engine_does_not_use_known_direct_platform_api_vocabulary()
    {
        // ProjectReference checks close the architectural door to adapters;
        // this bounded source vocabulary closes the BCL side doors that do not
        // require a project reference. It is deliberately narrower than a
        // claim that all System.IO is impure: in-memory streams are valid.
        var root = RepoRoot();
        var moduleRoots = PortableEngineProjects
            .Select(project => Path.Combine(root, "src", project))
            .ToArray();
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
            "ADR-001 requires the portable engine to use service seams rather than direct platform reach:\n"
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
                "The only outbound path. Product-owner-adopted rehearsal requirement R2-12 makes an endpoint off the "
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

    // D5 / D19 Q3: the portable engine is a contract, not an aspiration. CI now
    // proves the generated samples are byte-identical between Windows and Linux,
    // and that proof silently assumes the engine stays platform-neutral -- an
    // assumption nothing checked, because no test in this repository inspected a
    // target framework. A third project quietly acquiring a -windows TFM would
    // erode the portable core and the cross-platform gate would not notice.
    //
    // Scoped to src/ deliberately. Three test projects target Windows because the
    // shell they exercise does; that is correct and is not this rule's business.
    //
    // What this does NOT claim: for most of the graph, NuGet already refuses the
    // mistake. A project with portable consumers cannot become Windows-coupled --
    // restore fails with NU1201 before any test runs. This check earns its place
    // on the cases NU1201 cannot see: a leaf nothing references yet (today
    // Foundry.Inference.Local has no consumers at all), a project whose consumers
    // are themselves platform-coupled, a newly added project, and a refactor that
    // centralizes TargetFramework into Directory.Build.props -- which builds
    // perfectly and would hide the value from a naive reader. All four were
    // exercised before this test was committed.
    private static readonly Dictionary<string, string> PlatformCoupledProjects =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Foundry.App.WinForms"] =
                "The application shell itself: WinForms, printing, and the platform "
                + "surfaces a kiosk needs.",
            ["Foundry.Infrastructure.Windows"] =
                "The platform adapter behind the engine's service seams. It exists so "
                + "that everything else does not have to be Windows-coupled.",
        };

    // D19 Q1 and Q2: the layering rule is the project's own (plan 6.2, src/README.md,
    // ADR-001), but until now it was asserted a project at a time, and five of the
    // original fourteen were not asserted about at all. Scattered per-project rules are how
    // those five gaps opened; completeness is the whole content of the rule, so this
    // is one map rather than five more tests.
    //
    // Q2 -- "nothing references the application shell" -- falls out of an exact map,
    // and is additionally asserted below so it reads as a rule rather than as an
    // accident of the current data.
    private static readonly Dictionary<string, string[]> AllowedProjectReferences =
        new(StringComparer.Ordinal)
        {
            ["Foundry.Domain"] = [],
            ["Foundry.Contracts"] = ["Foundry.Domain"],
            ["Foundry.Inference.Abstractions"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.Application"] = ["Foundry.Contracts", "Foundry.Domain", "Foundry.Inference.Abstractions"],
            ["Foundry.Inference.AzureOpenAI"] = ["Foundry.Inference.Abstractions"],
            ["Foundry.Inference.Local"] = ["Foundry.Inference.Abstractions"],
            ["Foundry.Inference.Synthetic"] = ["Foundry.Inference.Abstractions"],
            ["Foundry.Infrastructure.Simulated"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.Infrastructure.Windows"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.Modules.BuiltIn"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.Modules.DeterministicPress"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.Rendering"] = ["Foundry.Contracts", "Foundry.Domain"],
            ["Foundry.ReviewPreview"] = ["Foundry.Contracts", "Foundry.Domain", "Foundry.Rendering"],
            ["Foundry.Storage"] = ["Foundry.Contracts", "Foundry.Domain", "Foundry.Rendering"],
            ["Foundry.App.WinForms"] =
            [
                "Foundry.Application",
                "Foundry.Inference.AzureOpenAI",
                "Foundry.Infrastructure.Windows",
                "Foundry.Modules.BuiltIn",
                "Foundry.Modules.DeterministicPress",
                "Foundry.Rendering",
                "Foundry.ReviewPreview",
                "Foundry.Storage",
            ],
        };

    private const string ApplicationShell = "Foundry.App.WinForms";

    [Fact]
    public void Every_project_declares_its_full_reference_set()
    {
        var root = RepoRoot();
        var found = new List<string>();
        var wrong = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(
                     Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var project = Path.GetFileNameWithoutExtension(csproj);
            found.Add(project);

            if (!AllowedProjectReferences.TryGetValue(project, out var allowed))
            {
                // A new project with no entry must fail rather than inherit silence.
                // This is the clause that stops the map going stale by addition.
                wrong.Add($"{project}: no declared reference set");
                continue;
            }

            // Read ProjectReference elements specifically. A text search for a
            // project name would also match InternalsVisibleTo, which three engine
            // projects grant to the shell -- the opposite direction, and not a
            // reference at all.
            var actual = XDocument.Load(csproj).Descendants("ProjectReference")
                .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
                .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
                .Where(name => name.Length > 0)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = allowed.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                wrong.Add(
                    $"{project}: declared [{string.Join(", ", expected)}] but references [{string.Join(", ", actual)}]");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "A project's references do not equal its declared set. Adding or removing an "
            + "edge is a layering decision; record it in AllowedProjectReferences:\n"
            + string.Join('\n', wrong));

        var vanished = AllowedProjectReferences.Keys.Where(p => !found.Contains(p)).ToArray();
        Assert.True(
            vanished.Length == 0,
            "The reference map names projects that no longer exist under src/:\n"
            + string.Join('\n', vanished));
    }

    [Fact]
    public void Nothing_references_the_application_shell()
    {
        // Q2 stated as a rule rather than left implicit in the map's data, so that a
        // future edit adding the shell to some project's set fails here too, with a
        // message about layering rather than about a mismatched list.
        var offenders = AllowedProjectReferences
            .Where(entry => !string.Equals(entry.Key, ApplicationShell, StringComparison.Ordinal))
            .Where(entry => entry.Value.Contains(ApplicationShell, StringComparer.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Nothing may reference {ApplicationShell}: the shell depends on the engine, "
            + "never the reverse.\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void Only_the_shell_and_the_platform_adapter_target_a_platform()
    {
        var root = RepoRoot();
        var coupled = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(
                     Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(csproj);
            var frameworks = document.Descendants()
                .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .SelectMany(e => e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();

            var project = Path.GetFileNameWithoutExtension(csproj);

            // A project that declares no target framework would slip past the check
            // below without ever being read. Say so rather than skipping it.
            Assert.True(
                frameworks.Length > 0,
                $"{project} declares no TargetFramework, so its platform coupling cannot be read.");

            if (frameworks.Any(framework => framework.Contains("-windows", StringComparison.OrdinalIgnoreCase)))
            {
                coupled.Add(project);
            }
        }

        var undeclared = coupled.Where(p => !PlatformCoupledProjects.ContainsKey(p)).ToArray();
        Assert.True(
            undeclared.Length == 0,
            "A project outside the declared platform-coupled set targets a platform. The "
            + "portable engine is what the cross-platform sample gate rests on; if this is "
            + "intended, add it to PlatformCoupledProjects with the reason:\n"
            + string.Join('\n', undeclared));

        // If a project stops being platform-coupled that is good news, and the list
        // should shrink to say so rather than keeping a name it no longer needs.
        var stalePlatform = PlatformCoupledProjects.Keys.Where(p => !coupled.Contains(p)).ToArray();
        Assert.True(
            stalePlatform.Length == 0,
            "The platform-coupled list names projects that no longer target a platform; "
            + "remove them:\n"
            + string.Join('\n', stalePlatform));
    }
}
