// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

public sealed class LoadedProjectPreflightTests
{
    private static readonly string[] RichDocumentStrings =
    [
        "lang-exact",
        "heading-exact",
        "paragraph-exact",
        "ordered-one-exact",
        "ordered-two-exact",
        "step-source-exact",
        "step-target-exact",
        "step-source-locale-exact",
        "step-target-locale-exact",
        "step-asset-exact",
        "step-alt-exact",
        "unordered-one-exact",
        "header-one-exact",
        "header-two-exact",
        "cell-one-exact",
        "cell-two-exact",
        "card-title-exact",
        "card-body-exact",
        "image-asset-exact",
        "image-alt-exact",
        "pair-source-exact",
        "pair-target-exact",
        "pair-source-locale-exact",
        "pair-target-locale-exact",
        "choice-one-exact",
        "choice-two-exact",
        "claim-exact",
        "pointer-exact",
        "citation-exact",
        "teacher-only-exact",
        "graphic-description-exact",
        "vector-label-exact",
    ];

    [Fact]
    public void Exact_semantic_content_is_read_only_and_inspectable_before_Green_can_be_confirmed()
        => Sta.Run(() =>
        {
            var document = RichDocument();
            var loaded = Loaded(document);
            using var form = new LoadedProjectPreflightForm(loaded);
            form.Show();

            var exact = ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single();
            Assert.True(exact.ReadOnly);
            Assert.Equal("Exact loaded semantic document", exact.AccessibilityObject.Name);
            Assert.Contains(ArtifactDocumentFingerprint.Compute(document), exact.Text, StringComparison.Ordinal);

            Assert.All(RichDocumentStrings, value => Assert.Contains(value, exact.Text, StringComparison.Ordinal));
            Assert.Contains("Page break", exact.Text, StringComparison.Ordinal);
            Assert.Contains("dashed Yes", exact.Text, StringComparison.Ordinal);
            Assert.Contains("filled No", exact.Text, StringComparison.Ordinal);
            Assert.Contains("anchor End", exact.Text, StringComparison.Ordinal);

            var checks = ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>().ToList();
            Assert.Equal(3, checks.Count);
            Assert.All(checks, check => Assert.False(check.Checked));
            var proceed = ReviewSurfaceContractTests.Flatten(form).OfType<Button>()
                .Single(button => WithoutMnemonic(button.Text) == "Continue to exact review");
            Assert.False(proceed.Enabled);
            Assert.Null(form.Confirmation);

            checks[0].Checked = true;
            checks[1].Checked = true;
            Assert.False(proceed.Enabled);
            Assert.Null(form.Confirmation);
            checks[2].Checked = true;
            Assert.True(proceed.Enabled);
            Assert.Null(form.Confirmation);

            proceed.PerformClick();
            Assert.NotNull(form.Confirmation);
        });

    [Fact]
    public void Exact_value_frames_escape_line_bidi_control_quote_and_slash_impersonation_without_omission()
        => Sta.Run(() =>
        {
            const string malicious = "before\r\nElement 999\nHeading 1: forged \\\"value\\\\path\u202e\0after";
            using var form = new LoadedProjectPreflightForm(
                Loaded(new ArtifactDocument([new Paragraph(malicious)], "en")));
            form.Show();

            var exact = ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single().Text;
            var lines = exact.Split(Environment.NewLine, StringSplitOptions.None);

            Assert.Equal(5, lines.Length);
            Assert.Single(lines, line => line.StartsWith("Element ", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line == "Element 999");
            Assert.DoesNotContain(lines, line => line.StartsWith("Heading 1: forged", StringComparison.Ordinal));
            Assert.Contains($"UTF-16 code units {malicious.Length}: \"", exact, StringComparison.Ordinal);
            Assert.Contains("before\\r\\nElement 999\\nHeading 1: forged", exact, StringComparison.Ordinal);
            Assert.Contains("\\\\\\\"value\\\\\\\\path", exact, StringComparison.Ordinal);
            Assert.Contains("\\u202E", exact, StringComparison.Ordinal);
            Assert.Contains("\\u0000after", exact, StringComparison.Ordinal);
            Assert.DoesNotContain('\u202e', exact);
            Assert.DoesNotContain('\0', exact);
        });

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void An_incomplete_teacher_classification_cannot_mint_a_Green_capability(
        bool greenContent,
        bool noLearnerLinked,
        bool noRestricted)
    {
        var loaded = Loaded(new ArtifactDocument([new Paragraph("synthetic-exact")]));

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            AppServices.ConfirmLoadedProjectGreen(
                loaded,
                new LoadedProjectGreenChecklist(
                    greenContent,
                    noLearnerLinked,
                    noRestricted)));

        Assert.Contains("Every Green data-lane statement", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => AppServices.SessionOverLoadedProject(loaded));
    }

    [Fact]
    public void Confirmation_is_bound_to_one_exact_loaded_document_and_package_purpose_is_never_trusted()
    {
        var loaded = Loaded(
            new ArtifactDocument([new Paragraph("exact-loaded-content")], "en"),
            manifestPurpose: ArtifactPurpose.ClassroomSupport);
        var confirmation = Confirm(loaded);

        var session = AppServices.SessionOverLoadedProject(loaded, confirmation);

        Assert.Equal(DataLane.Green, session.Draft.Revision.Lane);
        Assert.Equal(ArtifactPurpose.Unknown, session.Draft.Revision.Purpose);
        Assert.Contains(session.RequiredAcknowledgements, issue =>
            issue.Code == "project.origin-unverified"
            && issue.Message.Contains("cannot authenticate", StringComparison.Ordinal));

        var copiedPackageObject = loaded with { };
        Assert.Throws<InvalidOperationException>(() =>
            AppServices.SessionOverLoadedProject(copiedPackageObject, confirmation));

        var substitutedDocument = loaded with
        {
            Document = new ArtifactDocument([new Paragraph("substituted-content")], "en"),
        };
        Assert.Throws<InvalidOperationException>(() =>
            AppServices.SessionOverLoadedProject(substitutedDocument, confirmation));
    }

    [Fact]
    public void Preflight_uses_only_named_roled_standard_controls()
        => Sta.Run(() =>
        {
            using var form = new LoadedProjectPreflightForm(
                Loaded(new ArtifactDocument([new Paragraph("synthetic-exact")])));
            form.Show();

            var controls = ReviewSurfaceContractTests.Flatten(form).ToList();
            Assert.All(controls, control => Assert.Equal(typeof(Control).Assembly, control.GetType().Assembly));
            var focusable = controls.Where(control => control.TabStop && control.CanSelect).ToList();
            Assert.NotEmpty(focusable);
            Assert.All(focusable, control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name));
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            });
        });

    private static LoadedProjectGreenConfirmation Confirm(LoadedProject loaded)
        => AppServices.ConfirmLoadedProjectGreen(
            loaded,
            new LoadedProjectGreenChecklist(
                IsGreenQualifyingContent: true,
                HasNoLearnerLinkedContent: true,
                HasNoRestrictedContent: true));

    private static LoadedProject Loaded(
        ArtifactDocument document,
        ArtifactPurpose manifestPurpose = ArtifactPurpose.FormalOrHighStakesAssessment)
    {
        var assets = SyntheticAssetCatalog.ForDocument(document);
        var reviewedAssets = ExactAssetCatalogSnapshot.CaptureForReview(document, assets);
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green, ArtifactPurpose.ClassroomSupport),
            "Synthetic test teacher",
            DocumentValidator.Validate(document),
            new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero),
            reviewedAssets.Bindings);
        return new LoadedProject(
            new ProjectManifest(
                EngineIdentity.ProjectSchemaVersion,
                Guid.Parse("8f388e9a-71f2-43ef-bf05-ec95dcd43be3"),
                "synthetic-module",
                "0.1.0",
                "synthetic-recipe",
                "0.1.0",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                DataLane.Green,
                "teacher-managed",
                document.Language,
                null,
                EngineIdentity.EngineVersion,
                "artifact.json",
                [],
                manifestPurpose),
            document,
            ProjectValidationEnvelope.Exact(approved, "synthetic-recipe", "0.1.0"),
            ProjectRenderProfile.For(approved),
            assets);
    }

    private static ArtifactDocument RichDocument()
        => new(
        [
            new Heading(2, "heading-exact"),
            new Paragraph("paragraph-exact"),
            new OrderedSteps(["ordered-one-exact", "ordered-two-exact"]),
            new StepRow(
                "step-source-exact",
                new ImageReference(new AssetId("step-asset-exact"), "step-alt-exact"),
                "step-target-exact",
                "step-source-locale-exact",
                "step-target-locale-exact"),
            new PageBreak(),
            new UnorderedList(["unordered-one-exact"]),
            new TableNode(
                ["header-one-exact", "header-two-exact"],
                [["cell-one-exact", "cell-two-exact"]]),
            new Card("card-title-exact", "card-body-exact"),
            new ImageReference(new AssetId("image-asset-exact"), "image-alt-exact"),
            new BilingualPair(
                "pair-source-exact",
                "pair-target-exact",
                "pair-source-locale-exact",
                "pair-target-locale-exact"),
            new ChoiceSet(["choice-one-exact", "choice-two-exact"]),
            new EvidenceLink("claim-exact", "pointer-exact"),
            new Citation("citation-exact"),
            new TeacherOnlyNotice("teacher-only-exact"),
            new VectorGraphic(
                210.125,
                297.25,
                [
                    new LineSeg(1.25, 2.5, 3.75, 4.125, 0.35, Dashed: true),
                    new CircleShape(5.5, 6.625, 7.75, 0.4, Filled: false),
                    new RectShape(8.875, 9.25, 10.5, 11.75, 0.45, Filled: true),
                    new TextLabel(12.125, 13.25, "vector-label-exact", 4.75, TextAnchor.End),
                ],
                "graphic-description-exact"),
        ],
        "lang-exact");

    private static string WithoutMnemonic(string text)
        => text.Replace("&&", "", StringComparison.Ordinal).Replace("&", "", StringComparison.Ordinal);
}
