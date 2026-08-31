// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.CompilerServices;
using System.Text;
using Foundry.Modules.BuiltIn;

namespace Foundry.App.WinForms;

public enum UiLocaleMode
{
    Neutral,
    Pseudo,
    ReviewedCatalog,
}

/// <summary>
/// The app-chrome locale switch (handover 2026-08-29, forge item 4 — the
/// council's multilingual-stewardship directive). The pseudo-locale "ẋẋ"
/// stretches every string by at least forty percent, brackets it so truncation
/// confesses at a glance, and forces right-to-left mirroring — so layout and
/// mirroring defects surface before the multilingual seat's review, not during
/// it. A real catalog can activate only after the multilingual seat supplies
/// its text and review assertion; this plumbing does not grant that review.
/// </summary>
public static class UiLocale
{
    public const string PseudoSwitch = "--pseudo-locale";

    public const string PseudoEnvironmentVariable = "OCF_PSEUDO_LOCALE";

    public const string CatalogSwitch = "--ui-catalog";

    public const string CatalogEnvironmentVariable = "OCF_UI_CATALOG";

    public const string ExportTemplateSwitch = "--export-ui-catalog-template";

    private static ReviewedUiCatalog? _catalog;

    public static UiLocaleMode Mode { get; private set; }

    /// <summary>Canonical BCP-47 tag for active reviewed catalogs; "ẋẋ" marks the pseudo-locale.</summary>
    public static string LanguageTag => Mode switch
    {
        UiLocaleMode.Pseudo => "ẋẋ",
        UiLocaleMode.ReviewedCatalog => _catalog!.LanguageTag,
        _ => "en",
    };

    public static UiTextDirection TextDirection
        => Mode == UiLocaleMode.Pseudo || _catalog?.Direction == UiTextDirection.RightToLeft
            ? UiTextDirection.RightToLeft
            : UiTextDirection.LeftToRight;

    /// <summary>The active file's assertion; this is not authentication of the named human.</summary>
    public static UiCatalogReviewMetadata? ActiveReview => _catalog?.Review;

    public static UiCatalogProvenance? ActiveCatalogProvenance => _catalog?.Provenance;

    public static void Configure(string[] args)
        => ConfigureCore(args, UiCatalogDeployment.ApprovedCatalogSha256);

    /// <summary>
    /// Exercises the selector and loader with synthetic, exact-byte pins. Only
    /// friend test assemblies can supply these pins; production always uses
    /// the build's compiled deployment allowlist.
    /// </summary>
    internal static void ConfigureForTest(
        string[] args,
        IReadOnlySet<string> approvedCatalogSha256)
        => ConfigureCore(args, approvedCatalogSha256);

    private static void ConfigureCore(
        string[] args,
        IReadOnlySet<string> approvedCatalogSha256)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(approvedCatalogSha256);
        Reset();

        var catalogPath = SwitchValue(args, CatalogSwitch)
            ?? NullIfWhiteSpace(Environment.GetEnvironmentVariable(CatalogEnvironmentVariable));
        var pseudo = args.Contains(PseudoSwitch, StringComparer.Ordinal)
            || Environment.GetEnvironmentVariable(PseudoEnvironmentVariable) == "1";
        if (pseudo && catalogPath is not null)
        {
            throw new InvalidDataException(UiStrings.CatalogSelectorConflict);
        }

        if (catalogPath is not null)
        {
            _catalog = UiCatalogDeployment.LoadApproved(
                catalogPath,
                approvedCatalogSha256);
            Mode = UiLocaleMode.ReviewedCatalog;
        }
        else if (pseudo)
        {
            Mode = UiLocaleMode.Pseudo;
        }
    }

    /// <summary>Writes the neutral review packet and tells Program not to open a window.</summary>
    public static bool TryExportTemplate(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var path = SwitchValue(args, ExportTemplateSwitch);
        if (path is null)
        {
            return false;
        }

        if (args.Contains(PseudoSwitch, StringComparer.Ordinal)
            || SwitchValue(args, CatalogSwitch) is not null
            || Environment.GetEnvironmentVariable(PseudoEnvironmentVariable) == "1"
            || NullIfWhiteSpace(Environment.GetEnvironmentVariable(CatalogEnvironmentVariable)) is not null)
        {
            throw new InvalidDataException(UiStrings.CatalogExportSelectorConflict);
        }

        UiCatalogInventory.WriteTemplate(path);
        return true;
    }

    /// <summary>Test seam; production code configures from args and environment.</summary>
    public static void Set(UiLocaleMode mode)
    {
        if (mode == UiLocaleMode.ReviewedCatalog)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        _catalog = null;
        Mode = mode;
    }

    internal static string Translate(string id, string neutralFallback)
        => Mode == UiLocaleMode.ReviewedCatalog
            ? _catalog!.Translate(id, neutralFallback)
            : neutralFallback;

    /// <summary>
    /// The renderer forces dir="rtl" on right-to-left documents; this is the
    /// same discipline for WinForms chrome — under the pseudo-locale the whole
    /// window mirrors, so anything that only works left-to-right breaks loudly.
    /// </summary>
    public static void ApplyChrome(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var rightToLeft = TextDirection == UiTextDirection.RightToLeft;
        form.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;
        form.RightToLeftLayout = rightToLeft;
    }

    private static void Reset()
    {
        _catalog = null;
        Mode = UiLocaleMode.Neutral;
    }

    private static string? SwitchValue(string[] args, string switchName)
    {
        string? value = null;
        for (var index = 0; index < args.Length; index++)
        {
            string? candidate = null;
            if (string.Equals(args[index], switchName, StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(UiStrings.Format(UiStrings.CatalogSwitchValueMissing, switchName));
                }

                candidate = args[++index];
            }
            else if (args[index].StartsWith(switchName + "=", StringComparison.Ordinal))
            {
                candidate = args[index][(switchName.Length + 1)..];
            }

            if (candidate is null)
            {
                continue;
            }

            if (value is not null)
            {
                throw new InvalidDataException(UiStrings.Format(UiStrings.CatalogSwitchDuplicate, switchName));
            }

            value = NullIfWhiteSpace(candidate)
                ?? throw new InvalidDataException(UiStrings.Format(UiStrings.CatalogSwitchValueMissing, switchName));
        }

        return value;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
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
    private static readonly Lock ChromeLock = new();
    private static readonly Dictionary<string, string> ChromeFallbacks = new(StringComparer.Ordinal);

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

    public static string StatusAllAboardApprovedAccessHeld => T($"Approved for {ModulePublicIdentity.VisualSupport.DisplayName} outputs. Access Remix remains held until the protected specialist purpose authority is designed and reviewed; typed content cannot waive that hold.");

    public static string StatusNotApproved => T("Review ended without approval; nothing is unlocked.");

    public static string StatusSaved => T("Saved to the library as {0}.");

    public static string SavedArtifactContextMismatch => T("The saved validation context or render profile does not bind to this exact approved artifact.");

    public static string SavedProjectNeedsManagedUpgrade => T("This saved project predates exact validation context. Prepare a managed side-by-side compatible copy before reopening it.");

    public static string ProjectLibraryRootSwitchInvalid => T("The project-library-root switch is incomplete or repeated.");

    public static string ProjectLibraryRootInvalid => T("The project library root must be an absolute existing non-linked directory.");

    public static string ProjectLibraryRootVersionSegmentMissing => T("The project library root does not contain this engine's literal version segment.");

    public static string ProjectOutsideConfiguredLibrary => T("The selected project is outside the configured project library.");

    public static string SavedOriginUnverifiedWarning => T("This mutable package cannot authenticate its originating recipe, purpose, review notices, protected-seat review, or output settings. Treat its purpose as unknown; inspect every exact element, keep learner output selected unless teacher-only material is intended, and verify paper size and 100 percent calibration before printing. It cannot serve as an Access source.");

    public static string LoadedProjectPreflightWindowTitle => Compose(T("reopened project data-lane preflight"));

    public static string LoadedProjectPreflightIntroduction => T("A project package is editable, so its Green label is not evidence. Classify the content itself before exact Gate B review. If any statement is false, cancel; early-release outputs stay locked.");

    public static string LoadedProjectPreflightChecklist => T("Green data-lane classification");

    public static string LoadedProjectExactDocument => T("Exact loaded semantic document");

    public static string LoadedProjectDocumentDigest => T("Exact semantic document SHA-256: {0}");

    public static string LoadedProjectDocumentLanguage => T("Document language: {0}");

    public static string LoadedProjectDocumentElement => T("Element {0}");

    public static string ExactValueNotSet => T("(not set)");

    public static string LoadedProjectExactStringFrame => T("UTF-16 code units {0}: \"{1}\"");

    public static string BooleanYes => T("Yes");

    public static string BooleanNo => T("No");

    public static string TextAnchorStart => T("Start");

    public static string TextAnchorMiddle => T("Middle");

    public static string TextAnchorEnd => T("End");

    public static string SeverityInformation => T("Information");

    public static string SeverityWarning => T("Warning");

    public static string SeverityBlocking => T("Blocking");

    public static string LoadedProjectGreenContent => T("This exact project's content is generic, teacher-created, staged, openly licensed, public-domain, or otherwise authorized for this use.");

    public static string LoadedProjectNoLearnerLinkedContent => T("It contains no student work, handwriting, faces, voices, identifying or linkable learner information, or personalized family communication.");

    public static string LoadedProjectNoRestrictedContent => T("It contains no IEP/504, diagnosis, medical, counseling, behavioral, disciplinary, custody, private schedule, individualized AAC or communication, safety-disclosure, or recipient-list material.");

    public static string LoadedProjectPreflightExactBinding => T("This teacher classification applies only to the exact selected document. Any change requires a new classification and fresh review; it does not authenticate purpose, recipe, or protected-seat approval.");

    public static string ContinueToExactReview => T("&Continue to exact review");

    public static string Cancel => T("C&ancel");

    public static string LoadedProjectGreenChecklistIncomplete => T("Every Green data-lane statement must be confirmed for this exact document.");

    public static string StatusLoadedProjectPreflightNotConfirmed => T("The reopened project remains Amber by default; Green outputs stay locked until its exact data-lane preflight is completed.");

    public static string StatusExported => T("Exported to {0}.");

    public static string StatusExporting => T("Exporting {0}…");

    public static string StatusExportCancelled => T("Export cancelled; no completed output was reported.");

    public static string CancelExport => T("Cancel e&xport");

    public static string ExportDestinationNoParent => T("The export destination has no parent directory.");

    public static string ExportStageResidueNotRemoved => T("The export did not complete and its staged residue could not be removed.");

    public static string ExportStageRemainedAfterDeletion => T("The staged export remained after deletion.");

    public static string ExportStageCouldNotBeRemoved => T("The staged export could not be removed.");

    public static string SymbolCatalogIntegrityFailed => T("The shipped symbol catalog failed integrity validation: {0}.");

    public static string StatusPrintViewOpening => T("Opening the approved print view safely…");

    public static string StatusPrintView => T("Sent to your browser — verify the print view is visible, then print at 100 percent scale.");

    public static string StatusPrintViewRefused => T("The print view could not be opened safely. Try again, or export the approved output instead.");

    public static string PrintButton => T("&Print");

    public static string StatusPrinted => T("Sent to the printer; verify paper size and 100 percent scale with the calibration page.");

    public static string OpenFromLibrary => T("Open from &library…");

    public static string TileForWall => T("Tile for &wall…");

    public static string TileColumns => T("Columns");

    public static string TileRows => T("Rows");

    public static string TileMake => T("&Make tiles");

    public static string TileNeedsSingleSheet => T("wall tiling takes a single-sheet artifact");

    public static string BookletNeedsPages => T("a booklet needs at least two content pages");

    public static string ExportFilterBooklet => T("Booklet PDF (2-up saddle-stitch)");

    public static string ExportFilterPdf => T("PDF (local print pipeline)");

    public static string ExportFilterPrint => T("Print HTML");

    public static string ExportFilterAccessible => T("Accessible HTML");

    public static string ExportFilterSvg => T("SVG (single sheet only)");

    // SequenceSlate surface — stable legacy ids retain the former working name.
    public static string AllAboardOpen => T($"{ModulePublicIdentity.VisualSupport.DisplayName.Replace("q", "&q", StringComparison.Ordinal)}…");

    // Built-in module studios. Module/field labels have stable identifiers in
    // ModuleStudioCatalog; their neutral fallbacks pass through the same
    // chrome catalog until a seat-reviewed language pack is supplied.
    public static string BuiltInStudiosOpen => T("&Built-in studios…");

    public static string ImportAndVerifyBoard => T("&Import and verify board…");

    public static string ModuleStudioWindowTitle => Compose(T("built-in module studios"));

    public static string ModuleDoors => T("Module doors");

    public static string ModuleMode => T("Studio mode");

    public static string ModuleInputs => T("Module inputs");

    public static string ModuleLaneConfirmation => T("Data lane confirmation");

    public static string GreenInputAttestation => T("I confirm these inputs are staged, generic, teacher-created, or openly licensed — &Green");

    public static string ModuleNotes => T("Module notes and safeguards");

    public static string ModuleOutputOptions => T("Reviewed output options");

    public static string OutputAudience => T("Output audience");

    public static string AudienceTeacher => T("Teacher copy");

    public static string AudienceLearner => T("Learner copy");

    public static string TextScalePercent => T("Text scale percent");

    public static string TargetLanguageFirst => T("Target-&language first");

    public static string AccessPurposeAuthorityAbsent => T("Protected purpose authority is not available in this application");

    public static string ModuleLaneAndRecipe => T("Lane: {0}. Studio: {1}. Recipe version: {2}.");

    public static string ModuleSyntheticStarter => T("The starter values are synthetic and contain no learner data.");

    public static string ModuleSensitiveInput => T("This field can hold governed material. Follow the displayed lane and district safeguards.");

    public static string ModuleProhibitedPurpose => T("Refused purpose: {0}");

    public static string StatusModuleReady => T("Set the module inputs and reviewed output options, then review and approve.");

    public static string StatusModuleGreenRequired => T("Green confirmation is required. Unknown or learner-linked inputs remain Amber and cannot enter this studio.");

    public static string StatusModuleUnavailable => T("Unavailable: {0}");

    public static string StatusModuleBuiltWithIssues => T("The draft has {0} blocking issue(s); review announces them and approval remains unavailable.");

    public static string ModuleIssueDetail => T("{0}: {1}");

    public static string StatusModuleApproved => T("Approved — print, print view, export, and save are unlocked for these output options.");

    public static string StatusModuleNotApproved => T("Review ended without approval; no output is unlocked.");

    public static string StatusModuleChanged => T("An input or output option changed; fresh review and approval are required.");

    public static string ExportFilterModulePrint => T("Print HTML");

    public static string ExportFilterModuleAccessible => T("Accessible HTML");

    public static string RecordTableHint => T("Add rows as needed. Choice cells submit stable values even when their labels are translated.");

    // Board-to-Brief intake. This surface verifies source transcription and
    // reading order only; artifact creation, review, and output remain in the
    // built-in module studio.
    public static string BoardIntakeWindowTitle => Compose(T("Board to Brief — source intake"));

    public static string BoardIntakeIntroduction => T("Compare the normalized board image with every candidate word, resolve every uncertainty, then assign one role to every verified line. This intake creates no artifact and unlocks no output.");

    public static string BoardIntakeSourceImage => T("Normalized source image");

    public static string BoardIntakeCandidateText => T("OCR candidate text — not verified");

    public static string BoardIntakeVerifiedText => T("Teacher-verified text; unresolved words remain explicit");

    public static string BoardIntakeCurrentUncertain => T("Current uncertain word {0}: {1}");

    public static string BoardIntakeNoCurrentUncertain => T("No unresolved word is selected.");

    public static string BoardIntakeReplacement => T("Exact replacement text");

    public static string BoardIntakeNextUncertain => T("&Next uncertain");

    public static string BoardIntakeAcceptCandidate => T("&Accept candidate");

    public static string BoardIntakeRetype => T("&Retype exactly");

    public static string BoardIntakeMarkIllegible => T("Mark &illegible");

    public static string BoardIntakeManualTranscript => T("Manual literal-transcript fallback");

    public static string BoardIntakeManualInstructions => T("If local Windows OCR is unavailable or unsuitable, type one literal source line per line. Every entered line will still be marked uncertain and require explicit acceptance, retyping, or an illegible mark.");

    public static string BoardIntakeManualInput => T("Literal source lines");

    public static string BoardIntakeUseManual => T("Use &manual literal transcript");

    public static string BoardIntakeLineRoles => T("Verified line reading order and teacher-assigned roles");

    public static string BoardIntakeLineColumn => T("Verified line");

    public static string BoardIntakeRoleColumn => T("Role");

    public static string BoardIntakeRoleTitle => T("Title");

    public static string BoardIntakeRoleStep => T("Step");

    public static string BoardIntakeRoleMaterial => T("Material");

    public static string BoardIntakeRoleVocabulary => T("Vocabulary");

    public static string BoardIntakeRoleDate => T("Date");

    public static string BoardIntakeRoleNote => T("Teacher note");

    public static string BoardIntakeMoveLineUp => T("Move line &up");

    public static string BoardIntakeMoveLineDown => T("Move line &down");

    public static string BoardIntakeFinish => T("&Finish and return lines");

    public static string BoardIntakeCancel => T("&Cancel intake");

    public static string BoardIntakeUnresolvedMarker => T("[unresolved: {0}]");

    public static string StatusBoardIntakeStarting => T("Opening the capture and lane-confirmation surface…");

    public static string StatusBoardIntakeOcrRunning => T("Running local Windows OCR. Its candidates are not verified and every word without reported confidence requires teacher review.");

    public static string StatusBoardIntakeOcrReady => T("Local OCR candidates are ready. Resolve every uncertain word against the source image.");

    public static string StatusBoardIntakeOcrFallback => T("Local Windows OCR could not provide candidate text: {0} Use the honestly labeled manual literal-transcript fallback.");

    public static string StatusBoardIntakeAmberRefused => T("Board to Brief accepts only teacher-confirmed Green staged or openly licensed source material. Amber capture was refused and its bytes are being purged.");

    public static string StatusBoardIntakeManualRequired => T("Enter at least one nonblank literal source line before using the manual fallback.");

    public static string StatusBoardIntakeManualWhitespace => T("A manual source line cannot contain only whitespace. Remove the blank line or transcribe its visible content.");

    public static string StatusBoardIntakeManualLoaded => T("Manual literal lines are loaded as uncertain candidates. Verify each one against the source image.");

    public static string StatusBoardIntakeRetypeRequired => T("Type the exact replacement before choosing Retype exactly.");

    public static string StatusBoardIntakeRolesReady => T("Transcription is complete. Assign a role to every line, verify reading order, and choose Finish and return lines.");

    public static string StatusBoardIntakeRoleRequired => T("Every verified line must have one teacher-assigned role.");

    public static string StatusBoardIntakeOneTitleRequired => T("Exactly one verified line must be assigned the Title role.");

    public static string StatusBoardIntakeLineSelectionRequired => T("Select exactly one verified line before changing reading order.");

    public static string StatusBoardIntakeLineBoundary => T("The selected line is already at that reading-order boundary.");

    public static string StatusBoardIntakePurgeIncomplete => T("Normalized source bytes could not be fully purged. Only Retry secure purge is available; no verified lines have been returned.");

    public static string StatusBoardIntakeReturned => T("Source bytes were purged successfully. Verified lines are ready for the module table; Green confirmation and Gate B review remain required.");

    public static string StatusBoardIntakeGridError => T("The line-role table refused an invalid value. Select one of the listed roles.");

    public static string StatusBoardIntakeImageUnavailable => T("The normalized source image is unavailable; intake was refused.");

    public static string StatusBoardIntakeImported => T("Verified board lines were placed in the module table. Confirm their Green status again before Gate B review; no approval or output was carried across.");

    public static string StatusBoardIntakeTableUnavailable => T("The Board-to-Brief line table is unavailable. No intake rows were applied.");

    public static string StatusBoardIntakeRowsRefused => T("The intake handoff was invalid: it must contain nonblank verified lines, defined roles, and exactly one title. The module table was not changed.");

    public static string AllAboardWindowTitle => Compose(T($"{ModulePublicIdentity.VisualSupport.DisplayName} — drafting a task strip"));

    public static string TaskTitle => T("Task title");

    public static string StepTextLabel => T("Step {0} text");

    public static string StepSymbolLabel => T("Step {0} symbol");

    public static string NoSymbol => T("(no symbol)");

    public static string OutputMode => T("Output mode");

    public static string ModeTaskStrip => T("Task strip");

    public static string ModeFirstThen => T("First/Then");

    public static string ModeNowNextDone => T("Now/Next/Done");

    public static string ModeAgencyCards => T("Agency cards");

    public static string CardTextLabel => T("{0} card text");

    public static string CardSymbolLabel => T("{0} card symbol");

    public static string AgencyOverrideLabel => T("Label override for {0} (blank keeps the catalog meaning)");

    /// <summary>How a duplicated symbol meaning is told apart from its twin: meaning first, alt text after.</summary>
    public static string SymbolDisambiguation => T("{0} — {1}");

    public static string LowInkToggle => T("Low &ink");

    /// <summary>
    /// Stable-id chrome seam for press and module labels. Unknown ids fall
    /// back exactly; reviewed catalogs must contain every known inventory id.
    /// </summary>
    public static string Localize(string localizationId, string neutralFallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationId);
        ArgumentNullException.ThrowIfNull(neutralFallback);
        return UiLocale.Mode == UiLocaleMode.Pseudo
            ? Pseudoize(neutralFallback)
            : UiLocale.Translate(localizationId, neutralFallback);
    }

    // Review surface.
    public static string ReviewWindowTitle => Compose(T("reviewing a draft — nothing prints before approval"));

    public static string DraftElements => T("Draft elements");

    public static string ReviewElementsTab => T("Elements and &issues");

    public static string SourceComparisonTab => T("&Source comparison");

    public static string VisualPreviewTab => T("&Visual preview");

    public static string ExactSourceContext => T("Exact source or verified transcription");

    public static string ExactCurrentDraft => T("Exact current semantic draft");

    public static string SourceUnavailable => T("Source unavailable for this manual or reopened path. No source or transcription has been fabricated.");

    public static string ExactSourceCodeUnitCount => T("UTF-16 code units: {0}");

    public static string ExactSourceBegins => T("--- exact source begins ---");

    public static string ExactSourceEnds => T("--- exact source ends ---");

    public static string UnapprovedVisualPreview => T("Unapproved visual preview");

    public static string UnapprovedPreviewBrowser => T("Embedded unapproved visual derivative; printing, saving, navigation, and browser shortcuts are disabled");

    public static string UnapprovedPreviewStatus => T("UNAPPROVED DRAFT — preview only. This surface cannot print, export, or save the draft.");

    public static string UnapprovedPreviewLoading => T("UNAPPROVED DRAFT — loading the exact visual preview. Approval remains locked until this exact revision is displayed.");

    public static string PreviewImageNoDom => T("The preview image has no DOM element.");

    public static string UnapprovedPreviewProfile => T("Exact preview profile: {0}; {1}; text scale {2} percent; {3}.");

    public static string PreviewPrintLayout => T("print HTML layout");

    public static string PreviewAccessibleLayout => T("accessible HTML layout");

    public static string PreviewSourceLanguageFirst => T("source language first");

    public static string PreviewTargetLanguageFirst => T("target language first");

    public static string PreviewUnavailable => T("The exact visual derivative could not be created. Approval remains locked: {0}");

    public static string PreviewGenerationExhausted => T("The exact visual preview load identity was exhausted. Approval remains locked; close and reopen review.");

    public static string SelectedElementText => T("Selected element text");

    public static string ValidationIssues => T("Validation issues");

    public static string SelectedValidationIssueDetail => T("Selected validation issue detail");

    public static string ApplyEdit => T("&Apply edit");

    public static string EditElement => T("Edit &element…");

    public static string RemoveElement => T("&Remove element");

    public static string MoveUp => T("Move &up");

    public static string MoveDown => T("Move &down");

    public static string Approve => T("A&pprove");

    public static string Reject => T("Re&ject");

    public static string ApproveDescription => T("Records your named approval of this exact revision; only approved artifacts can print, save, or export.");

    public static string ReviewWarningsAcknowledgement => T("I have reviewed the non-dismissable &warnings");

    public static string ReviewWarningsAcknowledgementDescription => T("Required warnings must be acknowledged before approval.");

    public static string PendingEditMustBeAppliedOrRejected => T("Apply the pending edit or choose Reject to discard it before closing.");

    public static string NodeEditorWindowTitle => Compose(T("editing one exact draft element"));

    public static string ExactSelectedElementBeforeEdit => T("Exact selected element before edit");

    public static string TypedElementFields => T("Typed element fields");

    public static string ApplyReplacement => T("&Apply replacement");

    public static string DiscardReplacement => T("&Discard replacement");

    public static string PendingReplacementMustBeAppliedOrDiscarded => T("Apply this pending replacement or choose Discard replacement before closing.");

    public static string NodeEditorInvalidNumber => T("Enter a finite number for {0}; the replacement has not been applied.");

    public static string NodeEditorNoEditableFields => T("This element has no fields. It can still be moved or removed from the review surface.");

    public static string EditorHeadingLevel => T("Heading level");

    public static string EditorText => T("Text");

    public static string EditorCardTitle => T("Card title");

    public static string EditorCardBody => T("Card body");

    public static string EditorAssetIdentity => T("Asset identity");

    public static string EditorAlternativeText => T("Alternative text");

    public static string EditorSourceText => T("Source text");

    public static string EditorTargetText => T("Target text");

    public static string EditorSourceLocale => T("Source locale");

    public static string EditorTargetLocale => T("Target locale");

    public static string EditorClaim => T("Evidence claim");

    public static string EditorSourcePointer => T("Evidence source pointer");

    public static string EditorIncludeTranslation => T("Include aligned translation and locales");

    public static string EditorIncludeSymbol => T("Include step symbol");

    public static string EditorSequenceItems => T("Sequence items");

    public static string EditorItemText => T("Item text");

    public static string AddItem => T("Add &item");

    public static string RemoveItem => T("&Remove item");

    public static string MoveItemUp => T("Move item &up");

    public static string MoveItemDown => T("Move item do&wn");

    public static string EditorTableHasHeader => T("First row contains table headers");

    public static string EditorTableColumnCount => T("Table column count");

    public static string EditorTableCells => T("Table cells; the first row is the header when selected");

    public static string EditorTableColumn => T("Column {0}");

    public static string EditorTableHeaderRow => T("Header");

    public static string EditorTableDataRow => T("Row {0}");

    public static string AddTableRow => T("Add &row");

    public static string RemoveTableRow => T("Remo&ve row");

    public static string MoveTableRowUp => T("Move row &up");

    public static string MoveTableRowDown => T("Move row do&wn");

    public static string EditorVectorDescription => T("Vector graphic accessible description");

    public static string EditorVectorWidthMm => T("Vector graphic width in millimeters");

    public static string EditorVectorHeightMm => T("Vector graphic height in millimeters");

    public static string EditorVectorPrimitives => T("Vector primitives in drawing order");

    public static string EditorVectorPrimitiveType => T("Primitive type to add or replace");

    public static string AddPrimitive => T("Add pri&mitive");

    public static string ReplacePrimitiveType => T("Replace primitive &type");

    public static string RemovePrimitive => T("Remo&ve primitive");

    public static string MovePrimitiveUp => T("Move primitive &up");

    public static string MovePrimitiveDown => T("Move primitive do&wn");

    public static string ApplyPrimitiveEdit => T("A&pply primitive edit");

    public static string DiscardPrimitiveEdit => T("Dis&card primitive edit");

    public static string PrimitiveLine => T("Line");

    public static string PrimitiveCircle => T("Circle");

    public static string PrimitiveRectangle => T("Rectangle");

    public static string PrimitiveTextLabel => T("Text label");

    public static string EditorX1Mm => T("Start X in millimeters");

    public static string EditorY1Mm => T("Start Y in millimeters");

    public static string EditorX2Mm => T("End X in millimeters");

    public static string EditorY2Mm => T("End Y in millimeters");

    public static string EditorCenterXMm => T("Center X in millimeters");

    public static string EditorCenterYMm => T("Center Y in millimeters");

    public static string EditorXPositionMm => T("X position in millimeters");

    public static string EditorYPositionMm => T("Y position in millimeters");

    public static string EditorRadiusMm => T("Radius in millimeters");

    public static string EditorWidthMm => T("Width in millimeters");

    public static string EditorHeightMm => T("Height in millimeters");

    public static string EditorStrokeWidthMm => T("Stroke width in millimeters");

    public static string EditorDashed => T("Dashed line");

    public static string EditorFilled => T("Filled shape");

    public static string EditorLabelText => T("Vector label text");

    public static string EditorFontSizeMm => T("Font size in millimeters");

    public static string EditorTextAnchor => T("Text anchor");

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

    public static string NodeStepRow => T("Step row");

    public static string NodePageBreak => T("Page break");

    public static string NodeVectorGraphic => T("Vector graphic: {0}");

    public static string NodeTextContent => T("Text: {0}");

    public static string NodeBodyContent => T("Body: {0}");

    public static string NodeTranslationContent => T("Translation: {0}");

    public static string NodeLocalesContent => T("Locales: {0} to {1}");

    public static string NodeSymbolAltContent => T("Symbol alt text: {0}");

    public static string NodeImageAssetIdentity => T("Image asset id: {0}");

    public static string NodeDimensionsContent => T("Dimensions: {0} × {1} mm");

    public static string NodeVectorPrimitiveCounts => T("Vector primitives: {0} line(s), {1} circle(s), {2} rectangle(s), {3} text label(s)");

    public static string NodeVectorLineDetail => T("Line: ({0}, {1}) mm to ({2}, {3}) mm; stroke {4} mm; dashed {5}");

    public static string NodeVectorCircleDetail => T("Circle: center ({0}, {1}) mm; radius {2} mm; stroke {3} mm; filled {4}");

    public static string NodeVectorRectangleDetail => T("Rectangle: x {0} mm, y {1} mm, width {2} mm, height {3} mm; stroke {4} mm; filled {5}");

    public static string NodeVectorTextLabelDetail => T("Text label: x {0} mm, y {1} mm; text {2}; font {3} mm; anchor {4}");

    public static string NodeTableHeaderCell => T("Header {0}: {1}");

    public static string NodeTableCell => T("Row {0}, column {1}: {2}");

    public static string NodeTableNoHeaders => T("Headers: none");

    public static string NodeOrderedStepItem => T("Step {0}: {1}");

    public static string NodeListItem => T("List item {0}: {1}");

    public static string NodeChoiceItem => T("Choice {0}: {1}");

    public static string NodeSourcePointerContent => T("Source pointer: {0}");

    public static string IssueLine => T("{0}: {1}");

    // Capture surface.
    public static string CaptureWindowTitle => Compose(T("capture"));

    public static string ImportImage => T("&Import image…");

    public static string ImportImageSizeRefused => T("The selected image is empty or exceeds the 16 MiB capture limit.");

    public static string RetryNormalization => T("Retry &normalization");

    public static string RetrySecurePurge => T("Retry sec&ure purge");

    public static string Rotate90 => T("&Rotate 90°");

    public static string LaneGreen => T("Staged materials or empty space — &Green (my attestation)");

    public static string LaneAmber => T("May include learners or their work — keep &Amber");

    public static string ConfirmLane => T("&Confirm lane and continue");

    public static string SafetyPause => T("I saw something concerning — &pause here");

    public static string StatusImported => T("Imported and normalized: metadata stripped.");

    public static string StatusNormalizationRetry => T("Normalization was refused: {0} The image bytes were retained safely; retry normalization before continuing.");

    public static string StatusCaptureRefused => T("The capture operation was refused: {0}");

    public static string StatusRotated => T("Rotated.");

    public static string StatusLaneConfirmed => T("Lane confirmed: {0}.");

    public static string StatusPurgeIncomplete => T("Captured bytes could not be fully purged. Retry secure purge before closing.");

    public static string PauseCaption => T("Paused — for the supervising adult");

    public static string ImagesFilterLabel => T("Images");

    // Catalog startup and review-packet refusals. These stay in the neutral
    // catalog too: an invalid pack cannot be trusted to translate its own error.
    public static string CatalogSelectorConflict => T("Choose either the pseudo-locale or one reviewed UI catalog, not both.");

    public static string CatalogExportSelectorConflict => T("Template export cannot be combined with a locale selector.");

    public static string CatalogSwitchValueMissing => T("The {0} switch requires a file path.");

    public static string CatalogSwitchDuplicate => T("The {0} switch may be supplied only once.");

    public static string CatalogUnreadable => T("The UI catalog at {0} could not be read ({1}).");

    public static string UiaHarnessSwitchInvalid => T("The UI Automation harness request is invalid.");

    public static string UiaHarnessExportInvalid => T("The UI Automation harness export request is invalid.");

    public static string CatalogInvalidJson => T("The UI catalog is not strict JSON near line {0}, byte {1}.");

    public static string CatalogInvalidUnicode => T("The UI catalog contains a malformed Unicode string.");

    public static string CatalogUnsupportedSchema => T("The UI catalog schema version is unsupported: {0}.");

    public static string CatalogDraftRefused => T("This UI catalog is a draft. A named multilingual-seat review is required before activation.");

    public static string CatalogReviewStatusInvalid => T("The UI catalog review status is invalid: {0}.");

    public static string CatalogDirectionInvalid => T("The UI catalog direction must be exactly ltr or rtl, not {0}.");

    public static string CatalogReviewerMissing => T("The reviewed UI catalog must name its reviewer without surrounding whitespace.");

    public static string CatalogReviewerRoleInvalid => T("The reviewed UI catalog has the wrong reviewer role: {0}.");

    public static string CatalogReviewDateInvalid => T("The reviewed-at time must be canonical UTC in yyyy-MM-ddTHH:mm:ssZ form, not {0}.");

    public static string CatalogDigestMismatch => T("The reviewed UI catalog covers source digest {0}, but this build requires {1}.");

    public static string CatalogNeutralMissing => T("The neutral source table is missing string id {0}.");

    public static string CatalogNeutralUnknown => T("The neutral source table contains unknown string id {0}.");

    public static string CatalogNeutralChanged => T("The neutral source text changed for string id {0}; export a fresh packet.");

    public static string CatalogStringMissing => T("The translated string table is missing string id {0}.");

    public static string CatalogStringUnknown => T("The translated string table contains unknown string id {0}.");

    public static string CatalogStringBlank => T("The translated string is blank, padded, or contains a control character for string id {0}.");

    public static string CatalogPlaceholderMismatch => T("Format placeholders do not match the neutral source for string id {0}.");

    public static string CatalogMnemonicMismatch => T("Keyboard mnemonic markers do not match the neutral source for string id {0}.");

    public static string CatalogMnemonicDuplicate => T("Keyboard access key {0} is assigned more than once in the simultaneously visible {1} context: {2} and {3}.");

    public static string CatalogObjectRequired => T("The UI catalog value at {0} must be an object.");

    public static string CatalogPropertyUnknown => T("The UI catalog object {0} contains unknown property {1}.");

    public static string CatalogPropertyDuplicate => T("The UI catalog object {0} repeats property {1}.");

    public static string CatalogPropertyMissing => T("The UI catalog object {0} is missing property {1}.");

    public static string CatalogStringRequired => T("The UI catalog value at {0}.{1} must be a string.");

    public static string CatalogLanguageTagInvalid => T("The reviewed UI catalog language tag is not canonical or supported: {0}.");

    public static string CatalogFormatInvalid => T("A UI catalog source or translation has invalid format braces: {0}.");

    public static string CatalogProvenanceInvalid => T("The UI catalog provenance value is missing or malformed: {0}.");

    public static string CatalogArrayRequired => T("The UI catalog value at {0} must be an array.");

    public static string CatalogNotApprovedForBuild => T($"This exact UI catalog is not approved for this {ProductIdentity.PublicName} build. A command-line path or JSON review assertion cannot grant multilingual-seat approval.");

    public static string CatalogFileSizeInvalid => T("The UI catalog file is empty or exceeds the bounded catalog size.");

    public static string Format(string template, params object?[] arguments)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, template, arguments);

    /// <summary>
    /// Formats static catalog chrome for a control that does not process
    /// mnemonic markers. The template's '&amp;' contract is decoded before raw
    /// dynamic arguments are inserted, so neither side can reinterpret the
    /// other (for example, translated "Ready &amp;&amp; waiting: {0}" plus a file
    /// name containing one literal ampersand).
    /// </summary>
    public static string FormatWithoutMnemonic(string template, params object?[] arguments)
        => Format(WithoutMnemonic(template), arguments);

    public static string WithoutMnemonic(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                result.Append(text[index]);
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '&')
            {
                result.Append('&');
                index++;
            }
        }

        return result.ToString();
    }

    internal static IReadOnlyDictionary<string, string> NeutralChrome
    {
        get
        {
            // Calling every public string property makes T register its stable
            // member-name id and neutral fallback. No translated return value is
            // used here, so active locale state cannot contaminate the packet.
            foreach (var property in typeof(UiStrings).GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                    && property.GetMethod?.IsStatic == true
                    && property.GetIndexParameters().Length == 0))
            {
                _ = property.GetValue(null);
            }

            lock (ChromeLock)
            {
                return new SortedDictionary<string, string>(ChromeFallbacks, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>The public name never localizes (ADR-006); only the phrase beside it does.</summary>
    private static string Compose(string phrase) => $"{ProductIdentity.PublicName} — {phrase}";

    private static string T(string neutral, [CallerMemberName] string memberName = "")
    {
        var id = UiCatalogIds.Chrome(memberName);
        lock (ChromeLock)
        {
            if (ChromeFallbacks.TryGetValue(id, out var existing)
                && !string.Equals(existing, neutral, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(id);
            }

            ChromeFallbacks[id] = neutral;
        }

        return UiLocale.Mode == UiLocaleMode.Pseudo
            ? Pseudoize(neutral)
            : UiLocale.Translate(id, neutral);
    }

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
