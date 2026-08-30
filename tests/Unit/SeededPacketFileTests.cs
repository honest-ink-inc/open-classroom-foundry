// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.SampleGenerator;

namespace Foundry.Tests.Unit;

/// <summary>
/// The seeded-error study's packets are an input, not source (29 Aug 2026): a
/// blind study cannot define its seeds in a repository meant to be public. These
/// tests hold the loader's refusals, which a facilitator meets while hand-editing
/// the file — often shortly before a session, which is why they must read plainly.
/// </summary>
public class SeededPacketFileTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var path in _temporaryFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Temp cleanup is best-effort.
            }
        }
    }

    private string WriteJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"seeded-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _temporaryFiles.Add(path);
        return path;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }

    [Fact]
    public void The_committed_example_loads_and_is_the_documented_shape()
    {
        var example = Path.Combine(RepoRoot(), "docs", "evidence", "pilot-kit", "seeded-packets.example.json");
        Assert.True(File.Exists(example), "The committed example is the only definitions file this repository may hold.");

        var set = SeededPacketFile.Load(example);

        Assert.Equal(2, set.Packets.Count);
        Assert.All(set.Packets, packet => Assert.NotEmpty(packet.Steps));

        // The bilingual example exercises both optional fields — symbol and translation.
        var bilingual = set.Packets.Single(p => p.TargetLocale is not null);
        Assert.Equal("es", bilingual.TargetLocale);
        Assert.Contains(SeededPacketFile.ToStepSpecs(bilingual), step => step.Symbol is not null);
        Assert.All(SeededPacketFile.ToStepSpecs(bilingual), step => Assert.NotNull(step.TargetText));
    }

    [Fact]
    public void A_missing_definitions_file_names_where_the_real_one_lives()
    {
        var exception = Assert.Throws<FileNotFoundException>(
            () => SeededPacketFile.Load(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json")));

        Assert.Contains("outside this repository", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_and_empty_files_fail_loudly()
    {
        var malformed = WriteJson("{ not json");
        Assert.Throws<InvalidOperationException>(() => SeededPacketFile.Load(malformed));

        var empty = WriteJson("{ \"packets\": [] }");
        Assert.Contains("no packets", Assert.Throws<InvalidOperationException>(
            () => SeededPacketFile.Load(empty)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_duplicate_letter_is_refused_because_it_would_confound_the_matrix()
    {
        var path = WriteJson("""
            { "packets": [
                { "letter": "a", "title": "One", "steps": [ { "text": "Step one." }, { "text": "Step two." }, { "text": "Step three." } ] },
                { "letter": "a", "title": "Two", "steps": [ { "text": "Step one." }, { "text": "Step two." }, { "text": "Step three." } ] }
            ] }
            """);

        Assert.Contains("Two packets claim the letter", Assert.Throws<InvalidOperationException>(
            () => SeededPacketFile.Load(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_translation_without_a_locale_is_refused_in_both_directions()
    {
        // The renderer owns language tags; a second language with no tag is the
        // defect the multilingual seat would find, caught here where it is cheap.
        var untagged = WriteJson("""
            { "packets": [
                { "letter": "a", "title": "Untagged", "steps": [ { "text": "Step one.", "targetText": "Paso uno." }, { "text": "Step two." }, { "text": "Step three." } ] }
            ] }
            """);
        Assert.Contains("declares no targetLocale", Assert.Throws<InvalidOperationException>(
            () => SeededPacketFile.Load(untagged)).Message, StringComparison.Ordinal);

        var emptyPromise = WriteJson("""
            { "packets": [
                { "letter": "a", "title": "Promised", "targetLocale": "es", "steps": [ { "text": "Step one." }, { "text": "Step two." }, { "text": "Step three." } ] }
            ] }
            """);
        Assert.Contains("carries no translation", Assert.Throws<InvalidOperationException>(
            () => SeededPacketFile.Load(emptyPromise)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_packet_outside_the_task_strip_bounds_is_refused_by_name()
    {
        // The builder enforces 3-8; the loader says it first, and says which packet.
        var tooFew = WriteJson("""
            { "packets": [
                { "letter": "a", "title": "Thin", "steps": [ { "text": "Only step." } ] }
            ] }
            """);

        var message = Assert.Throws<InvalidOperationException>(() => SeededPacketFile.Load(tooFew)).Message;
        Assert.Contains("Packet 'a' has 1 steps", message, StringComparison.Ordinal);
        Assert.Contains("3 to 8", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Letters_name_packets_and_carry_nothing_else()
    {
        var path = WriteJson("""
            { "packets": [
                { "letter": "seeded", "title": "Tell-tale", "steps": [ { "text": "Step." } ] }
            ] }
            """);

        Assert.Contains("not a single letter", Assert.Throws<InvalidOperationException>(
            () => SeededPacketFile.Load(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_definitions_file_but_the_example_is_tracked_in_this_repository()
    {
        // The structural guard: if a real definitions file or a facilitator key
        // ever lands in the tree, this test says so before a publish does.
        var kit = Path.Combine(RepoRoot(), "docs", "evidence", "pilot-kit");

        Assert.False(File.Exists(Path.Combine(kit, "seeded-packets.json")),
            "Real seeded-packet definitions must never be committed; they train every reader.");
        Assert.False(File.Exists(Path.Combine(kit, "FACILITATOR-KEY.md")),
            "The facilitator key must never be committed; it is the study's answer sheet.");
    }
}
