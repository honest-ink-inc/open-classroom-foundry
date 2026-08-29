// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace Foundry.App.WinForms;

public enum UiLocaleMode
{
    Neutral,
    Pseudo,
}

/// <summary>
/// The app-chrome locale switch (handover 2026-08-29, forge item 4 — the
/// council's multilingual-stewardship directive). The pseudo-locale "ẋẋ"
/// stretches every string by at least forty percent, brackets it so truncation
/// confesses at a glance, and forces right-to-left mirroring — so layout and
/// mirroring defects surface before the multilingual seat's review, not during
/// it. Real translations arrive as additional catalogs; the mechanism is ready.
/// </summary>
public static class UiLocale
{
    public const string PseudoSwitch = "--pseudo-locale";

    public const string PseudoEnvironmentVariable = "OCF_PSEUDO_LOCALE";

    public static UiLocaleMode Mode { get; private set; }

    /// <summary>BCP-47-shaped tag for diagnostics and future catalogs; "ẋẋ" marks the pseudo-locale.</summary>
    public static string LanguageTag => Mode == UiLocaleMode.Pseudo ? "ẋẋ" : "en";

    public static void Configure(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Mode = args.Contains(PseudoSwitch, StringComparer.Ordinal)
            || Environment.GetEnvironmentVariable(PseudoEnvironmentVariable) == "1"
            ? UiLocaleMode.Pseudo
            : UiLocaleMode.Neutral;
    }

    /// <summary>Test seam; production code configures from args and environment.</summary>
    public static void Set(UiLocaleMode mode) => Mode = mode;

    /// <summary>
    /// The renderer forces dir="rtl" on right-to-left documents; this is the
    /// same discipline for WinForms chrome — under the pseudo-locale the whole
    /// window mirrors, so anything that only works left-to-right breaks loudly.
    /// </summary>
    public static void ApplyChrome(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (Mode == UiLocaleMode.Pseudo)
        {
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
        }
    }
}

/// <summary>
/// The single catalog of user-facing chrome strings — the architecture test
/// forbids any such literal elsewhere in this assembly. The product's public
/// name (ADR-006) is a name, never localized; domain-produced text (validation
/// messages, safeguarding procedures, artifact content) is document content,
/// localized by its own contracts, not by this chrome catalog.
/// </summary>
public static class UiStrings
{
    // Main surface — the Press Room. The subtitle's neutral source lives in
    // ProductIdentity (ADR-006's single name record); localization routes here.
    public static string MainWindowTitle => Compose(T(ProductIdentity.Subtitle));

    public static string PressList => T("Presses");

    public static string BudgetLine => T("Declared time-to-artifact budget: {0} minutes.");

    public static string ReviewAndApprove => T("&Review and approve…");

    public static string OpenPrintView => T("&Open print view");

    public static string ExportEllipsis => T("&Export…");

    public static string SaveToLibrary => T("&Save to library");

    public static string StatusReady => T("Choose a press, set its parameters, then review and approve.");

    public static string StatusRefused => T("The press refused: {0}");

    public static string StatusApproved => T("Approved — print view, export, and save to library are unlocked.");

    public static string StatusNotApproved => T("Review ended without approval; nothing is unlocked.");

    public static string StatusSaved => T("Saved to the library as {0}.");

    public static string StatusExported => T("Exported to {0}.");

    public static string StatusPrintView => T("Print view opened in your browser — print at 100 percent scale.");

    public static string PrintButton => T("&Print");

    public static string StatusPrinted => T("Sent to the printer at 100 percent scale.");

    public static string OpenFromLibrary => T("Open from &library…");

    public static string TileForWall => T("Tile for &wall…");

    public static string TileColumns => T("Columns");

    public static string TileRows => T("Rows");

    public static string TileMake => T("&Make tiles");

    public static string TileNeedsSingleSheet => T("wall tiling takes a single-sheet artifact");

    public static string BookletNeedsPages => T("a booklet needs at least two content pages");

    public static string ExportFilterBooklet => T("Booklet PDF (2-up saddle-stitch)");

    public static string ExportFilterPdf => T("PDF (vector sheets)");

    public static string ExportFilterPrint => T("Print HTML");

    public static string ExportFilterAccessible => T("Accessible HTML");

    public static string ExportFilterSvg => T("SVG (single sheet only)");

    // All Aboard surface — the ratified 0.1 typed-steps slice only.
    public static string AllAboardOpen => T("&All Aboard task strip…");

    public static string AllAboardWindowTitle => Compose(T("All Aboard — drafting a task strip"));

    public static string TaskTitle => T("Task title");

    public static string StepTextLabel => T("Step {0} text");

    public static string StepSymbolLabel => T("Step {0} symbol");

    public static string NoSymbol => T("(no symbol)");

    /// <summary>Module-supplied neutral text (press titles, parameter labels) rendered through the active locale.</summary>
    public static string Localize(string neutral)
    {
        ArgumentNullException.ThrowIfNull(neutral);
        return T(neutral);
    }

    // Review surface.
    public static string ReviewWindowTitle => Compose(T("reviewing a draft — nothing prints before approval"));

    public static string DraftElements => T("Draft elements");

    public static string SelectedElementText => T("Selected element text");

    public static string ValidationIssues => T("Validation issues");

    public static string ApplyEdit => T("&Apply edit");

    public static string RemoveElement => T("&Remove element");

    public static string MoveUp => T("Move &up");

    public static string MoveDown => T("Move &down");

    public static string Approve => T("A&pprove");

    public static string Reject => T("Re&ject");

    public static string ApproveDescription => T("Records your named approval of this exact revision; only approved artifacts can print, save, or export.");

    public static string SplitterDraftEditor => T("Splitter between the draft list and the editor");

    public static string SplitterEditorIssues => T("Splitter between the editor and the validation issues");

    public static string NodeHeading => T("Heading {0}: {1}");

    public static string NodeParagraph => T("Paragraph: {0}");

    public static string NodeSteps => T("Steps ({0})");

    public static string NodeList => T("List ({0})");

    public static string NodeTable => T("Table ({0} rows)");

    public static string NodeCard => T("Card: {0}");

    public static string NodeImage => T("Image: {0}");

    public static string NodeBilingual => T("Bilingual: {0}");

    public static string NodeChoices => T("Choices ({0})");

    public static string NodeEvidence => T("Evidence: {0}");

    public static string NodeCitation => T("Citation: {0}");

    public static string NodeTeacherOnly => T("Teacher-only: {0}");

    public static string IssueLine => T("{0}: {1}");

    // Capture surface.
    public static string CaptureWindowTitle => Compose(T("capture"));

    public static string ImportImage => T("&Import image…");

    public static string Rotate90 => T("&Rotate 90°");

    public static string LaneGreen => T("Staged materials or empty space — &Green (my attestation)");

    public static string LaneAmber => T("May include learners or their work — keep &Amber");

    public static string ConfirmLane => T("&Confirm lane and continue");

    public static string SafetyPause => T("I saw something concerning — &pause here");

    public static string StatusImported => T("Imported and normalized: metadata stripped.");

    public static string StatusRotated => T("Rotated.");

    public static string StatusLaneConfirmed => T("Lane confirmed: {0}.");

    public static string PauseCaption => T("Paused — for the supervising adult");

    public static string ImagesFilterLabel => T("Images");

    public static string Format(string template, params object[] arguments)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, template, arguments);

    /// <summary>The public name never localizes (ADR-006); only the phrase beside it does.</summary>
    private static string Compose(string phrase) => $"{ProductIdentity.PublicName} — {phrase}";

    private static string T(string neutral)
        => UiLocale.Mode == UiLocaleMode.Pseudo ? Pseudoize(neutral) : neutral;

    /// <summary>
    /// Deterministic pseudo-localization: accents most letters, keeps format
    /// placeholders and the mnemonic character intact (Alt+key still works),
    /// pads by at least forty percent of the letter count, and brackets the
    /// whole string so a truncated end is visible in any review or screenshot.
    /// </summary>
    private static string Pseudoize(string neutral)
    {
        var builder = new StringBuilder("⟦");
        var letters = 0;

        for (var i = 0; i < neutral.Length; i++)
        {
            var ch = neutral[i];
            if (ch == '{')
            {
                var close = neutral.IndexOf('}', i);
                var end = close < 0 ? neutral.Length - 1 : close;
                builder.Append(neutral, i, end - i + 1);
                i = end;
                continue;
            }

            if (ch == '&' && i + 1 < neutral.Length)
            {
                builder.Append('&').Append(neutral[i + 1]);
                letters++;
                i++;
                continue;
            }

            if (char.IsLetter(ch))
            {
                letters++;
            }

            builder.Append(Accent(ch));
        }

        return builder
            .Append(' ')
            .Append('ẋ', Math.Max(2, (int)Math.Ceiling(letters * 0.4)))
            .Append('⟧')
            .ToString();
    }

    private static char Accent(char ch) => ch switch
    {
        'a' => 'á',
        'e' => 'é',
        'i' => 'í',
        'o' => 'ó',
        'u' => 'ú',
        'y' => 'ý',
        'c' => 'ç',
        'n' => 'ñ',
        'x' => 'ẋ',
        'A' => 'Á',
        'E' => 'É',
        'I' => 'Í',
        'O' => 'Ó',
        'U' => 'Ú',
        'Y' => 'Ý',
        'C' => 'Ç',
        'N' => 'Ñ',
        'X' => 'Ẋ',
        _ => ch,
    };
}
