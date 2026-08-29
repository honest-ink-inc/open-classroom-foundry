// SPDX-License-Identifier: GPL-3.0-or-later
// Renders the repository's governing documents to the honest-ink.org static
// site artifact. Deterministic: same documents, byte-identical pages.
// Publishing the output is the typist's act, never this tool's.
// Usage: SiteGenerator <repoRoot> <outputDirectory>

using Foundry.Tools.SiteGenerator;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: SiteGenerator <repoRoot> <outputDirectory>");
    return 1;
}

Directory.CreateDirectory(args[1]);
foreach (var (fileName, content) in SiteBuilder.Build(args[0]))
{
    await File.WriteAllBytesAsync(Path.Combine(args[1], fileName), content);
}

Console.WriteLine($"Site written to {args[1]} — publishing it remains a human act.");
return 0;
