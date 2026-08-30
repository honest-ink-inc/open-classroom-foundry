// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;

namespace Foundry.Tests.UiAutomation;

// The in-process half of the accessibility harness (ADR-002): what WinForms
// will hand to UI Automation, asserted as data — accessible names, roles, tab
// order, mnemonics, and the standard-controls rule. Test names carry the
// walkthrough-script step they encode (docs/accessibility/nvda-walkthrough-
// script.md); the mapping lives in docs/accessibility/uia-harness-traceability.md.
// What only a human ear can judge — actual speech — stays with the walkthrough.

public class ReviewSurfaceContractTests
{
    private static void WithReviewForm(Action<ReviewForm> assert) => Sta.Run(() =>
    {
        using var form = UiaHarness.CreateReviewForm();
        form.Show();
        assert(form);
    });

    [Fact]
    public void Part1_Step1_the_window_title_announces_the_product_and_the_draft_state()
        => WithReviewForm(form =>
        {
            Assert.Contains(ProductIdentity.PublicName, form.Text, StringComparison.Ordinal);
            Assert.Contains("draft", form.Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void Part1_Step2_every_focusable_control_announces_a_name_and_a_role_with_no_unnamed_pane()
        => WithReviewForm(form =>
        {
            // WinForms insists SplitContainers stay focusable (keyboard resize),
            // so the "no unnamed pane" rule is: everything selectable is named.
            var focusable = Flatten(form).Where(c => c.TabStop && c.CanSelect).ToList();

            Assert.NotEmpty(focusable);
            Assert.All(focusable, control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                    $"{control.GetType().Name} is an unnamed focusable control — the 'unnamed pane' failure of walkthrough step 2");
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            });
        });

    [Fact]
    public void Part1_Step2_the_action_buttons_tab_in_their_visual_order()
        => WithReviewForm(form => Assert.Equal(
            ["Apply edit", "Edit element…", "Remove element", "Move up", "Move down", "Approve", "Reject"],
            Flatten(form).OfType<FlowLayoutPanel>().Single().Controls
                .OfType<Button>()
                .OrderBy(c => c.TabIndex)
                .Select(c => c.AccessibilityObject.Name)
                .ToList()));

    [Fact]
    public void Part2_Step7_a_blank_step_surfaces_an_announced_issue_and_disables_approval()
        => Sta.Run(() =>
        {
            using var form = new ReviewForm(SessionOver(
                new Paragraph("Water each plant once."),
                new Paragraph("   ")));
            form.Show();

            var issues = (ListBox)ByName(form, "Validation issues");
            Assert.Contains(issues.Items.Cast<string>(), i => i.Contains("no text", StringComparison.Ordinal));

            // A disabled approval is announced as unavailable — the gate is audible.
            Assert.False(ByName(form, "Approve").Enabled);
        });

    [Fact]
    public void Part3_Step9_moving_a_step_keeps_selection_on_it_so_the_new_position_is_announced()
        => WithReviewForm(form =>
        {
            var list = (ListBox)ByName(form, "Draft elements");
            list.SelectedIndex = 1;
            var moved = (string)list.Items[1];

            ((Button)ByName(form, "Move down")).PerformClick();

            // Selection follows the moved element: the list's announced position
            // ("3 of 5") is the element's NEW position, which is step 9's demand.
            Assert.Equal(2, list.SelectedIndex);
            Assert.Equal(moved, list.Items[2]);
        });

    [Fact]
    public void Part3_Step10_the_edit_field_is_labeled_and_an_edit_reads_back_from_the_list()
        => WithReviewForm(form =>
        {
            var list = (ListBox)ByName(form, "Draft elements");
            var editor = (TextBox)ByName(form, "Selected element text");
            list.SelectedIndex = 2;
            Assert.Equal("Fill it to the line.", editor.Text);

            editor.Text = "Fill it exactly to the line.";
            ((Button)ByName(form, "Apply edit")).PerformClick();

            Assert.Equal("Paragraph: Fill it exactly to the line.", list.Items[2]);
        });

    [Fact]
    public void GateB_selected_content_exposes_every_consequential_field_of_every_nonparagraph_node()
        => Sta.Run(() =>
        {
            var samples = new (DocumentNode Node, string Expected)[]
            {
                (new Heading(3, "Exact heading"), "Heading 3: Exact heading"),
                (new OrderedSteps(["Open the lid.", "Pour exactly once."]), ExactLines(
                    "Steps (2)",
                    "Step 1: Open the lid.",
                    "Step 2: Pour exactly once.")),
                (new UnorderedList(["Blue", "Green"]), ExactLines(
                    "List (2)",
                    "List item 1: Blue",
                    "List item 2: Green")),
                (new TableNode(
                    ["Material", "Count"],
                    [["Cup", "2"], ["Spoon", "1"]]), ExactLines(
                        "Table (2 rows)",
                        "Header 1: Material",
                        "Header 2: Count",
                        "Row 1, column 1: Cup",
                        "Row 1, column 2: 2",
                        "Row 2, column 1: Spoon",
                        "Row 2, column 2: 1")),
                (new Card("Exact title", "Exact body"), ExactLines(
                    "Card: Exact title",
                    "Body: Exact body")),
                (new ImageReference(new AssetId("symbol.water-can"), "Water can symbol"), ExactLines(
                    "Image: Water can symbol",
                    "Image asset id: symbol.water-can")),
                (new BilingualPair(
                    "Close the lid.",
                    "Cierra la tapa.",
                    "en-US",
                    "es-US"), ExactLines(
                        "Text: Close the lid.",
                        "Translation: Cierra la tapa.",
                        "Locales: en-US to es-US")),
                (new ChoiceSet(["Draw", "Write", "Point"]), ExactLines(
                    "Choices (3)",
                    "Choice 1: Draw",
                    "Choice 2: Write",
                    "Choice 3: Point")),
                (new EvidenceLink("The mixture changed color.", "authorized-input:page-2#line-4"), ExactLines(
                    "Evidence: The mixture changed color.",
                    "Source pointer: authorized-input:page-2#line-4")),
                (new Citation("District source, page 7."), "Citation: District source, page 7."),
                (new TeacherOnlyNotice("Keep this answer key off learner pages."),
                    "Teacher-only: Keep this answer key off learner pages."),
                (new StepRow(
                    "Lift the blue card.",
                    new ImageReference(new AssetId("symbol.blue-card"), "Blue card"),
                    "Levanta la tarjeta azul.",
                    "en-US",
                    "es-US"), ExactLines(
                        "Step row",
                        "Text: Lift the blue card.",
                        "Translation: Levanta la tarjeta azul.",
                        "Locales: en-US to es-US",
                        "Image asset id: symbol.blue-card",
                        "Symbol alt text: Blue card")),
                (new PageBreak(), "Page break"),
            };

            foreach (var (node, expected) in samples)
            {
                using var form = new ReviewForm(SessionOver(node));
                form.Show();

                var editor = (TextBox)ByName(form, "Selected element text");
                Assert.True(editor.ReadOnly, $"{node.GetType().Name} exact content must be read-only");
                Assert.Equal(expected, editor.Text);
            }
        });

    [Fact]
    public void GateB_vector_content_exposes_description_dimensions_and_every_primitive_in_original_order()
        => Sta.Run(() =>
        {
            var graphic = new VectorGraphic(
                210.125,
                297.25,
                [
                    new LineSeg(1.25, 2.5, 3.75, 4.125, 0.35, Dashed: true),
                    new CircleShape(5.5, 6.625, 7.75, 0.4, Filled: true),
                    new RectShape(8.875, 9.25, 10.5, 11.75, 0.45, Filled: false),
                    new TextLabel(12.125, 13.25, "x + y = 4", 4.75, TextAnchor.End),
                ],
                "Exact coordinate sheet");

            using var form = new ReviewForm(SessionOver(graphic));
            form.Show();

            var editor = (TextBox)ByName(form, "Selected element text");
            Assert.True(editor.ReadOnly);
            Assert.Equal(ExactLines(
                "Vector graphic: Exact coordinate sheet",
                "Dimensions: 210.125 × 297.25 mm",
                "Vector primitives: 1 line(s), 1 circle(s), 1 rectangle(s), 1 text label(s)",
                "Line: (1.25, 2.5) mm to (3.75, 4.125) mm; stroke 0.35 mm; dashed Yes",
                "Circle: center (5.5, 6.625) mm; radius 7.75 mm; stroke 0.4 mm; filled Yes",
                "Rectangle: x 8.875 mm, y 9.25 mm, width 10.5 mm, height 11.75 mm; stroke 0.45 mm; filled No",
                "Text label: x 12.125 mm, y 13.25 mm; text x + y = 4; font 4.75 mm; anchor End"), editor.Text);
        });

    [Fact]
    public void GateB_typed_editor_replaces_all_bilingual_fields_through_standard_controls()
        => Sta.Run(() =>
        {
            using var editor = new NodeEditorForm(new BilingualPair(
                "Open the lid.",
                "Abre la tapa.",
                "en-US",
                "es-US"));
            editor.Show();

            EditorControl<TextBox>(editor, "Source text").Text = "Close the lid.";
            EditorControl<TextBox>(editor, "Target text").Text = "Cierra la tapa.";
            EditorControl<TextBox>(editor, "Source locale").Text = "en-GB";
            EditorControl<TextBox>(editor, "Target locale").Text = "es-MX";
            EditorButton(editor, "Apply replacement").PerformClick();

            var result = Assert.IsType<BilingualPair>(editor.Result);
            Assert.Equal("Close the lid.", result.SourceText);
            Assert.Equal("Cierra la tapa.", result.TargetText);
            Assert.Equal("en-GB", result.SourceLocale);
            Assert.Equal("es-MX", result.TargetLocale);
        });

    [Fact]
    public void GateB_blocks_malformed_language_tags_entered_through_both_typed_locale_editors()
        => Sta.Run(() =>
        {
            var originalPair = new BilingualPair("Open the lid.", "Abre la tapa.", "en", "es");
            var pairSession = SessionOver(originalPair);
            using (var pairEditor = new NodeEditorForm(originalPair))
            {
                pairEditor.Show();
                EditorControl<TextBox>(pairEditor, "Source locale").Text = "e n";
                EditorButton(pairEditor, "Apply replacement").PerformClick();
                pairSession.ReplaceNode(0, Assert.IsType<BilingualPair>(pairEditor.Result));
            }

            Assert.Contains(pairSession.Issues, issue => issue.Code == "doc.bilingual.locale-tag");
            Assert.False(pairSession.CanApprove);

            var originalStep = new StepRow(
                "Open the lid.",
                TargetText: "Abre la tapa.",
                SourceLocale: "en",
                TargetLocale: "es");
            var stepSession = SessionOver(originalStep);
            using (var stepEditor = new NodeEditorForm(originalStep))
            {
                stepEditor.Show();
                EditorControl<TextBox>(stepEditor, "Target locale").Text = "not_a_tag";
                EditorButton(stepEditor, "Apply replacement").PerformClick();
                stepSession.ReplaceNode(0, Assert.IsType<StepRow>(stepEditor.Result));
            }

            Assert.Contains(stepSession.Issues, issue => issue.Code == "doc.step-row.locale-tag");
            Assert.False(stepSession.CanApprove);
        });

    [Fact]
    public void GateB_typed_editor_replaces_every_table_cell_and_row_order()
        => Sta.Run(() =>
        {
            using var editor = new NodeEditorForm(new TableNode(
                ["Material", "Count"],
                [["Cup", "1"], ["Spoon", "2"]]));
            editor.Show();

            var grid = EditorControl<DataGridView>(editor, "Table cells; the first row is the header when selected");
            grid.Rows[0].Cells[0].Value = "Resource";
            grid.Rows[1].Cells[1].Value = "4";
            grid.CurrentCell = grid.Rows[2].Cells[0];
            EditorButton(editor, "Move row up").PerformClick();
            EditorButton(editor, "Apply replacement").PerformClick();

            var result = Assert.IsType<TableNode>(editor.Result);
            Assert.Equal(["Resource", "Count"], result.HeaderRow);
            Assert.Equal(["Spoon", "2"], result.Rows[0]);
            Assert.Equal(["Cup", "4"], result.Rows[1]);
        });

    [Fact]
    public void GateB_typed_editor_replaces_step_translation_and_symbol_metadata_without_flattening_them()
        => Sta.Run(() =>
        {
            using var editor = new NodeEditorForm(new StepRow("Lift the card."));
            editor.Show();

            EditorControl<TextBox>(editor, "Text").Text = "Lift the blue card.";
            EditorControl<CheckBox>(editor, "Include aligned translation and locales").Checked = true;
            EditorControl<TextBox>(editor, "Target text").Text = "Levanta la tarjeta azul.";
            EditorControl<TextBox>(editor, "Source locale").Text = "en-US";
            EditorControl<TextBox>(editor, "Target locale").Text = "es-US";
            EditorControl<CheckBox>(editor, "Include step symbol").Checked = true;
            EditorControl<TextBox>(editor, "Asset identity").Text = "symbol.blue-card.v2";
            EditorControl<TextBox>(editor, "Alternative text").Text = "Blue card symbol";
            EditorButton(editor, "Apply replacement").PerformClick();

            var result = Assert.IsType<StepRow>(editor.Result);
            Assert.Equal("Lift the blue card.", result.Text);
            Assert.Equal("Levanta la tarjeta azul.", result.TargetText);
            Assert.Equal("en-US", result.SourceLocale);
            Assert.Equal("es-US", result.TargetLocale);
            Assert.Equal("symbol.blue-card.v2", result.Symbol?.Asset.Value);
            Assert.Equal("Blue card symbol", result.Symbol?.AltText);
        });

    [Fact]
    public void GateB_typed_editor_edits_each_vector_primitive_in_place_before_replacing_the_node()
        => Sta.Run(() =>
        {
            using var editor = new NodeEditorForm(new VectorGraphic(
                100,
                80,
                [
                    new LineSeg(1, 2, 3, 4, 0.35, false),
                    new CircleShape(5, 6, 7, 0.4, false),
                    new RectShape(8, 9, 10, 11, 0.45, false),
                    new TextLabel(12, 13, "Original", 4.5, TextAnchor.Middle),
                ],
                "Original vector"));
            editor.Show();

            EditorControl<TextBox>(editor, "Vector graphic width in millimeters").Text = "210.5";
            EditorControl<TextBox>(editor, "Vector graphic accessible description").Text = "Reviewed vector";
            var primitives = EditorControl<ListBox>(editor, "Vector primitives in drawing order");

            EditorControl<TextBox>(editor, "Start X in millimeters").Text = "1.25";
            EditorControl<CheckBox>(editor, "Dashed line").Checked = true;
            EditorButton(editor, "Apply primitive edit").PerformClick();

            primitives.SelectedIndex = 1;
            EditorControl<TextBox>(editor, "Radius in millimeters").Text = "7.75";
            EditorControl<CheckBox>(editor, "Filled shape").Checked = true;
            EditorButton(editor, "Apply primitive edit").PerformClick();

            primitives.SelectedIndex = 2;
            EditorControl<TextBox>(editor, "Width in millimeters").Text = "10.5";
            EditorControl<CheckBox>(editor, "Filled shape").Checked = true;
            EditorButton(editor, "Apply primitive edit").PerformClick();

            primitives.SelectedIndex = 3;
            EditorControl<TextBox>(editor, "Vector label text").Text = "x + y = 4";
            EditorControl<ComboBox>(editor, "Text anchor").SelectedIndex = 2;
            EditorButton(editor, "Apply primitive edit").PerformClick();
            EditorButton(editor, "Apply replacement").PerformClick();

            var result = Assert.IsType<VectorGraphic>(editor.Result);
            Assert.Equal(210.5, result.WidthMm);
            Assert.Equal("Reviewed vector", result.Description);
            Assert.Equal(new LineSeg(1.25, 2, 3, 4, 0.35, true), Assert.IsType<LineSeg>(result.Primitives[0]));
            Assert.Equal(new CircleShape(5, 6, 7.75, 0.4, true), Assert.IsType<CircleShape>(result.Primitives[1]));
            Assert.Equal(new RectShape(8, 9, 10.5, 11, 0.45, true), Assert.IsType<RectShape>(result.Primitives[2]));
            Assert.Equal(
                new TextLabel(12, 13, "x + y = 4", 4.5, TextAnchor.End),
                Assert.IsType<TextLabel>(result.Primitives[3]));
        });

    [Fact]
    public void GateB_every_admitted_node_type_opens_a_standard_control_typed_editor()
        => Sta.Run(() =>
        {
            DocumentNode[] nodes =
            [
                new Heading(1, "Heading"),
                new Paragraph("Paragraph"),
                new OrderedSteps(["One"]),
                new UnorderedList(["One"]),
                new TableNode(["Header"], [["Cell"]]),
                new Card("Title", "Body"),
                new ImageReference(new AssetId("symbol.synthetic"), "Synthetic symbol"),
                new BilingualPair("Source", "Target", "en", "es"),
                new ChoiceSet(["One", "Two"]),
                new EvidenceLink("Claim", "authorized:line-1"),
                new Citation("Synthetic citation"),
                new TeacherOnlyNotice("Synthetic teacher note"),
                new StepRow("Step"),
                new PageBreak(),
                new VectorGraphic(10, 10, [new LineSeg(0, 0, 1, 1)], "Synthetic vector"),
            ];

            foreach (var node in nodes)
            {
                using var editor = new NodeEditorForm(node);
                editor.Show();

                Assert.NotNull(EditorButton(editor, "Apply replacement"));
                Assert.All(Flatten(editor), control => Assert.Equal(
                    typeof(Control).Assembly,
                    control.GetType().Assembly));
            }
        });

    [Fact]
    public void GateB_typed_editor_never_silently_discards_a_pending_replacement()
        => Sta.Run(() =>
        {
            using var editor = new NodeEditorForm(new EvidenceLink(
                "Original claim",
                "authorized:page-1"));
            editor.Show();
            EditorControl<TextBox>(editor, "Evidence source pointer").Text = "authorized:page-2";

            editor.Close();

            Assert.True(editor.Visible);
            Assert.Null(editor.Result);
            Assert.Contains(
                "Apply this pending replacement",
                Flatten(editor).OfType<Label>().Single(label =>
                    label.AccessibilityObject.Role == AccessibleRole.StatusBar).Text,
                StringComparison.Ordinal);

            EditorButton(editor, "Discard replacement").PerformClick();
            Assert.Null(editor.Result);
            Assert.Equal(DialogResult.Cancel, editor.DialogResult);
        });

    [Fact]
    public void GateB_review_applies_the_modal_result_only_through_a_new_exact_session_revision()
        => Sta.Run(() =>
        {
            var warning = ValidationIssue.Warning(
                "synthetic.required-warning",
                "Synthetic warning must be acknowledged again after every edit.",
                requiresAcknowledgement: true);
            var session = AppServices.SessionOver(
                DraftArtifact.New(
                    new ArtifactDocument([
                        new BilingualPair("Open.", "Abre.", "en-US", "es-US"),
                    ]),
                    DataLane.Green),
                new ReviewNoticeValidator(new DefaultArtifactValidator(), [warning]));
            using var review = new ReviewForm(session);
            review.Show();
            EditorControl<CheckBox>(review, "I have reviewed the non-dismissable warnings").Checked = true;
            Assert.True(session.CanApprove);
            var displayedRevision = session.Draft.Revision;

            review.BeginInvoke(() =>
            {
                var modal = System.Windows.Forms.Application.OpenForms
                    .Cast<Form>()
                    .OfType<NodeEditorForm>()
                    .Single();
                EditorControl<TextBox>(modal, "Target text").Text = "Cierra.";
                EditorButton(modal, "Apply replacement").PerformClick();
            });
            EditorButton(review, "Edit element…").PerformClick();

            Assert.NotSame(displayedRevision, session.Draft.Revision);
            Assert.Equal(displayedRevision.Number + 1, session.Draft.Revision.Number);
            Assert.Equal(
                "Cierra.",
                Assert.IsType<BilingualPair>(session.Draft.Revision.Document.Nodes[0]).TargetText);
            Assert.False(session.CanApprove);
            Assert.False(EditorControl<CheckBox>(review, "I have reviewed the non-dismissable warnings").Checked);
            Assert.Null(review.Result);
        });

    [Fact]
    public void GateB_dirty_paragraph_cannot_be_approved_selected_away_destroyed_or_silently_closed_before_apply()
        => Sta.Run(() =>
        {
            var session = SessionOver(
                new Paragraph("Original exact text."),
                new Paragraph("Second paragraph."));
            using var form = new ReviewForm(session);
            form.Show();

            var list = (ListBox)ByName(form, "Draft elements");
            var editor = (TextBox)ByName(form, "Selected element text");
            var apply = (Button)ByName(form, "Apply edit");
            var approve = (Button)ByName(form, "Approve");
            var remove = (Button)ByName(form, "Remove element");
            var moveDown = (Button)ByName(form, "Move down");

            Assert.False(editor.ReadOnly);
            editor.Text = "Changed exact text.";

            Assert.False(list.Enabled);
            Assert.False(remove.Enabled);
            Assert.False(moveDown.Enabled);
            Assert.False(approve.Enabled);
            Assert.Equal("Original exact text.", ((Paragraph)session.Draft.Revision.Document.Nodes[0]).Text);

            list.SelectedIndex = 1;
            Assert.Equal(0, list.SelectedIndex);
            Assert.Equal("Changed exact text.", editor.Text);

            approve.PerformClick();
            Assert.Null(form.Result);
            Assert.True(form.Visible);

            form.Close();
            Assert.True(form.Visible);
            Assert.False(form.IsDisposed);
            Assert.Equal(
                "Apply the pending edit or choose Reject to discard it before closing.",
                Flatten(form).OfType<Label>().Single(
                    control => control.AccessibilityObject.Name ==
                        "Apply the pending edit or choose Reject to discard it before closing.").Text);

            apply.PerformClick();
            Assert.Equal("Changed exact text.", ((Paragraph)session.Draft.Revision.Document.Nodes[0]).Text);
            Assert.True(list.Enabled);
            Assert.True(remove.Enabled);
            Assert.True(moveDown.Enabled);
            AwaitApprovalReady(form);
            Assert.True(approve.Enabled);

            approve.PerformClick();
            Assert.NotNull(form.Result);
            Assert.Equal(
                "Changed exact text.",
                ((Paragraph)form.Result.Revision.Document.Nodes[0]).Text);
        });

    [Fact]
    public void GateB_clean_system_close_cancels_the_in_flight_review()
        => Sta.Run(() =>
        {
            var session = SessionOver(new Paragraph("Synthetic exact text."));
            using var form = new ReviewForm(session);
            form.Show();

            form.Close();

            Assert.Equal(JobState.Cancelled, session.Machine.State);
            Assert.Null(session.ApprovedResult);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
        });

    [Fact]
    public void GateB_required_warning_is_fully_wrapped_selectable_and_visible_before_acknowledgement()
        => Sta.Run(() =>
        {
            var completeMessage = string.Join(' ', Enumerable.Repeat(
                "Synthetic exact required warning remains fully inspectable at the minimum floor.",
                12));
            var warning = ValidationIssue.Warning(
                "synthetic.required-warning",
                completeMessage,
                requiresAcknowledgement: true);
            var session = AppServices.SessionOver(
                DraftArtifact.New(
                    new ArtifactDocument([new Paragraph("Synthetic exact content.")]),
                    DataLane.Green),
                new ReviewNoticeValidator(new DefaultArtifactValidator(), [warning]));
            using var form = new ReviewForm(session)
            {
                Size = new Size(720, 480),
            };
            form.Show();

            var detail = (TextBox)ByName(form, "Selected validation issue detail");
            var acknowledge = (CheckBox)ByName(form, "I have reviewed the non-dismissable warnings");
            var approve = (Button)ByName(form, "Approve");

            Assert.True(detail.ReadOnly);
            Assert.True(detail.Multiline);
            Assert.True(detail.WordWrap);
            Assert.Contains(completeMessage, detail.Text, StringComparison.Ordinal);
            Assert.True(acknowledge.Visible);
            Assert.Equal('w', Mnemonic(acknowledge.Text));
            Assert.Equal(
                "I have reviewed the non-dismissable warnings",
                acknowledge.AccessibilityObject.Name);
            Assert.False(approve.Enabled);
        });

    [Fact]
    public void Part3_Step11_the_approval_control_states_what_approval_means()
        => WithReviewForm(form =>
        {
            var approve = ByName(form, "Approve");
            Assert.Contains("named approval", approve.AccessibleDescription, StringComparison.Ordinal);
            Assert.Contains("revision", approve.AccessibleDescription, StringComparison.Ordinal);
        });

    [Fact]
    public void Part3_Step12_approving_produces_the_typed_artifact_and_closes_as_accepted()
        => WithReviewForm(form =>
        {
            AwaitApprovalReady(form);
            ((Button)ByName(form, "Approve")).PerformClick();

            Assert.NotNull(form.Result);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });

    [Fact]
    public void Mnemonics_are_unique_so_every_action_has_one_unambiguous_access_key()
        => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();
            using var pressRoom = new PressRoomForm(
                reviewRunner: _ => null,
                libraryPicker: () => null,
                loadedProjectPreflight: _ => null);
            using var allAboard = new AllAboardForm(new AppServices.NoAssetsCatalog(), _ => null);
            using var modules = new ModuleStudioForm(reviewRunner: _ => null);
            using var editor = new NodeEditorForm(new Paragraph("Synthetic editor content."));
            using var tile = new TileForm();

            foreach (var form in new Form[] { review, capture, pressRoom, allAboard, modules, editor, tile })
            {
                var mnemonics = Flatten(form)
                    .OfType<ButtonBase>().Cast<Control>()
                    .Concat(Flatten(form).OfType<RadioButton>())
                    .Distinct()
                    .Select(c => Mnemonic(c.Text))
                    .OfType<char>()
                    .ToList();

                Assert.Equal(mnemonics.Count, mnemonics.Distinct().Count());
            }
        });

    [Fact]
    public void ADR002_standard_controls_only_no_custom_control_without_its_own_peer()
        => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();

            foreach (var form in new Form[] { review, capture })
            {
                Assert.All(Flatten(form), control => Assert.Equal(
                    typeof(Control).Assembly,
                    control.GetType().Assembly));
            }
        });

    internal static ReviewSession SessionOver(params DocumentNode[] nodes)
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return new ReviewSession(
            DraftArtifact.New(new ArtifactDocument(nodes), DataLane.Green),
            machine,
            new DefaultArtifactValidator());
    }

    internal static IReadOnlyList<Control> TabStops(Form form)
    {
        var stops = new List<Control>();
        Control? current = form;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (current.TabStop && current.CanSelect)
            {
                stops.Add(current);
            }
        }

        return stops;
    }

    internal static Control ByName(Form form, string accessibleName)
        => Flatten(form).Single(c => c.AccessibilityObject.Name == accessibleName);

    private static void AwaitApprovalReady(Form form)
    {
        var approve = (Button)ByName(form, "Approve");
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && !approve.Enabled)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(
            approve.Enabled,
            "Approval did not unlock after the exact current preview completed. "
                + string.Join(
                    " | ",
                    Flatten(form).OfType<Label>()
                        .Where(label => label.AccessibleRole == AccessibleRole.StatusBar)
                        .Select(label => label.Text)));
    }

    private static T EditorControl<T>(Form form, string accessibleName) where T : Control
        => Flatten(form).OfType<T>().Single(control =>
            control.AccessibilityObject.Name == accessibleName);

    private static Button EditorButton(Form form, string accessibleName)
        => EditorControl<Button>(form, accessibleName);

    internal static IEnumerable<Control> Flatten(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static char? Mnemonic(string text)
    {
        var index = text.Replace("&&", "  ", StringComparison.Ordinal).IndexOf('&', StringComparison.Ordinal);
        return index >= 0 && index + 1 < text.Length ? char.ToLowerInvariant(text[index + 1]) : null;
    }

    private static string ExactLines(params string[] lines)
        => string.Join(Environment.NewLine, lines);
}

public class CaptureSurfaceContractTests
{
    private static void WithCaptureForm(Action<CaptureForm> assert) => Sta.Run(() =>
    {
        using var form = UiaHarness.CreateCaptureForm();
        form.Show();
        assert(form);
    });

    [Fact]
    public void Part1_Step2_every_capture_tab_stop_announces_a_name_and_a_role()
        => WithCaptureForm(form => Assert.All(
            ReviewSurfaceContractTests.TabStops(form),
            control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name));
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            }));

    [Fact]
    public void Part4_Step14_the_lane_radios_carry_their_full_meaning_in_the_accessible_name()
        => WithCaptureForm(form =>
        {
            var radios = ReviewSurfaceContractTests.Flatten(form).OfType<RadioButton>().ToList();
            Assert.Equal(2, radios.Count);

            var green = Assert.Single(radios, r => r.AccessibilityObject.Name!.Contains("Green", StringComparison.Ordinal));
            var amber = Assert.Single(radios, r => r.AccessibilityObject.Name!.Contains("Amber", StringComparison.Ordinal));

            // Meaning in the name itself, not only in adjacent visual text.
            Assert.Contains("Staged materials", green.AccessibilityObject.Name, StringComparison.Ordinal);
            Assert.Contains("learners", amber.AccessibilityObject.Name, StringComparison.Ordinal);
        });

    [Fact]
    public void Part4_Step15_the_safety_pause_is_reachable_within_a_few_tabs_and_names_its_purpose()
        => WithCaptureForm(form =>
        {
            var stops = ReviewSurfaceContractTests.TabStops(form);
            var pauseIndex = stops.ToList().FindIndex(c =>
                c.AccessibilityObject.Name?.Contains("pause", StringComparison.OrdinalIgnoreCase) == true);

            Assert.InRange(pauseIndex, 0, 5); // "reachable in ≤ a few tabs"
            Assert.Contains("concerning", stops[pauseIndex].AccessibilityObject.Name, StringComparison.Ordinal);
        });
}
