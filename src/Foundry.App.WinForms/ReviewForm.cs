// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Runtime.InteropServices;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.App.WinForms;

/// <summary>
/// Gate B review surface. Standard controls only (ADR-002): every authoring and
/// approval behavior lives in <see cref="ReviewSession"/>; this form binds it.
/// Source/current-draft comparison and the visual derivative are explicit tabs.
/// The latter uses a sealed, visibly unapproved, in-process preview capability,
/// never the ApprovedArtifact-only output renderer/sinks (ADR-004).
/// </summary>
public sealed class ReviewForm : Form
{
    private readonly ReviewSession _session;
    private readonly TabControl _reviewTabs;
    private readonly ListBox _nodeList;
    private readonly TextBox _editor;
    private readonly ListBox _issueList;
    private readonly TextBox _issueDetail;
    private readonly TextBox _sourceContext;
    private readonly TextBox _currentDraft;
    private readonly Label _previewStatus;
    private readonly Label _previewProfile;
    private readonly WebBrowser _previewBrowser;
    private readonly Label _editStatus;
    private readonly Button _applyEdit;
    private readonly Button _editElement;
    private readonly Button _remove;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly CheckBox _acknowledgeWarnings;
    private readonly Button _approve;
    private readonly Button _reject;
    private readonly PreviewReadinessState _previewReadiness = new();
    private bool _editorDirty;
    private bool _loadingEditor;
    private bool _refreshingNodes;
    private bool _refreshingAcknowledgement;
    private bool _explicitClose;
    private int _loadedIndex = -1;

    public ReviewForm(ReviewSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));

        // The title is the first thing a screen reader announces: the draft
        // state must be audible there, not only visual (walkthrough step 8).
        Text = UiStrings.ReviewWindowTitle;
        MinimumSize = new Size(720, 480);

        _nodeList = new ListBox { Dock = DockStyle.Fill, AccessibleName = UiStrings.DraftElements };
        _nodeList.SelectedIndexChanged += (_, _) => SelectionChanged();

        _editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AccessibleName = UiStrings.SelectedElementText,
        };
        _editor.TextChanged += (_, _) => EditorTextChanged();

        _issueList = new ListBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.ValidationIssues,
            HorizontalScrollbar = true,
        };
        _issueList.SelectedIndexChanged += (_, _) => LoadIssueDetail();
        _issueDetail = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            AccessibleName = UiStrings.SelectedValidationIssueDetail,
        };
        _editStatus = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 36,
            AccessibleName = UiStrings.PendingEditMustBeAppliedOrRejected,
            AccessibleRole = AccessibleRole.StatusBar,
            Visible = false,
        };

        _applyEdit = MakeButton(UiStrings.ApplyEdit, (_, _) => ApplyEdit());
        _editElement = MakeButton(UiStrings.EditElement, (_, _) => EditSelectedElement());
        _remove = MakeButton(UiStrings.RemoveElement, (_, _) => WithSelection(_session.RemoveNode));
        _moveUp = MakeButton(UiStrings.MoveUp, (_, _) => MoveSelection(-1));
        _moveDown = MakeButton(UiStrings.MoveDown, (_, _) => MoveSelection(+1));
        _acknowledgeWarnings = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.ReviewWarningsAcknowledgement,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ReviewWarningsAcknowledgement),
            AccessibleDescription = UiStrings.ReviewWarningsAcknowledgementDescription,
        };
        _acknowledgeWarnings.CheckedChanged += (_, _) => AcknowledgementChanged();
        _approve = MakeButton(UiStrings.Approve, (_, _) => ApproveAndClose());
        // Approval must state what it means, not just "OK" (walkthrough step 11).
        _approve.AccessibleDescription = UiStrings.ApproveDescription;
        _reject = MakeButton(UiStrings.Reject, (_, _) => RejectAndClose());

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(
            [_applyEdit, _editElement, _remove, _moveUp, _moveDown, _acknowledgeWarnings, _approve, _reject]);

        // WinForms keeps SplitContainers keyboard-focusable by design (arrow keys
        // resize the split) and ignores TabStop=false — so the walkthrough's "no
        // unnamed pane" rule (step 2) is met by NAMING them, not by hiding them.
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            AccessibleName = UiStrings.SplitterDraftEditor,
        };
        split.Panel1.Controls.Add(_nodeList);

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            AccessibleName = UiStrings.SplitterEditorIssues,
        };
        rightSplit.Panel1.Controls.Add(_editor);
        rightSplit.Panel1.Controls.Add(_editStatus);
        var issues = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        issues.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        issues.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        issues.Controls.Add(_issueList, 0, 0);
        issues.Controls.Add(_issueDetail, 0, 1);
        rightSplit.Panel2.Controls.Add(issues);
        split.Panel2.Controls.Add(rightSplit);

        var elementsPage = TabPage(UiStrings.ReviewElementsTab);
        elementsPage.Controls.Add(split);

        _sourceContext = ReadOnlyTextBox(UiStrings.ExactSourceContext);
        _currentDraft = ReadOnlyTextBox(UiStrings.ExactCurrentDraft);
        var comparison = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
        };
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.Controls.Add(Group(UiStrings.ExactSourceContext, _sourceContext), 0, 0);
        comparison.Controls.Add(Group(UiStrings.ExactCurrentDraft, _currentDraft), 1, 0);
        var comparisonPage = TabPage(UiStrings.SourceComparisonTab);
        comparisonPage.Controls.Add(comparison);

        _previewStatus = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6),
            Text = UiStrings.UnapprovedPreviewStatus,
            AccessibleName = UiStrings.UnapprovedPreviewStatus,
            AccessibleRole = AccessibleRole.StatusBar,
        };
        _previewProfile = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6),
        };
        _previewBrowser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.UnapprovedVisualPreview,
            AccessibleDescription = UiStrings.UnapprovedPreviewBrowser,
            AllowWebBrowserDrop = false,
            IsWebBrowserContextMenuEnabled = false,
            WebBrowserShortcutsEnabled = false,
            ScriptErrorsSuppressed = true,
            ScrollBarsEnabled = true,
        };
        _previewBrowser.Navigating += PreviewBrowserNavigating;
        _previewBrowser.DocumentCompleted += PreviewBrowserDocumentCompleted;

        var previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        previewPanel.Controls.Add(_previewBrowser);
        previewPanel.Controls.Add(_previewProfile);
        previewPanel.Controls.Add(_previewStatus);
        var previewPage = TabPage(UiStrings.VisualPreviewTab);
        previewPage.Controls.Add(previewPanel);

        _reviewTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.ReviewWindowTitle,
        };
        _reviewTabs.TabPages.AddRange([elementsPage, comparisonPage, previewPage]);

        Controls.Add(_reviewTabs);
        Controls.Add(buttons);

        UiLocale.ApplyChrome(this);
        Refresh(selectIndex: 0);
    }

    /// <summary>Set when the teacher approves; null when the review ends any other way.</summary>
    public ApprovedArtifact? Result { get; private set; }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private static TabPage TabPage(string text)
        => new()
        {
            Text = text,
            AccessibleName = UiStrings.WithoutMnemonic(text),
            UseVisualStyleBackColor = true,
        };

    private static TextBox ReadOnlyTextBox(string accessibleName)
        => new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Both,
            AccessibleName = accessibleName,
        };

    private static GroupBox Group(string text, Control content)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = text,
            AccessibleName = text,
            Padding = new Padding(6),
        };
        group.Controls.Add(content);
        return group;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The system close button must not silently throw away text that has
        // not become a draft revision. Reject remains an explicit discard.
        if (_editorDirty && !_explicitClose)
        {
            e.Cancel = true;
            _editStatus.Text = UiStrings.PendingEditMustBeAppliedOrRejected;
            _editStatus.Visible = true;
            _applyEdit.Focus();
            // The legacy embedded browser tears its active document down while
            // Form.Close is unwinding even when this close is cancelled. Run
            // after that native teardown; if Apply landed meanwhile, restore
            // and verify the exact new revision. A still-dirty editor remains
            // fail-closed and will refresh only when Apply commits it.
            BeginInvoke(() =>
            {
                if (!IsDisposed && !Disposing && !_editorDirty)
                {
                    RefreshComparisonAndPreview();
                    UpdateActionAvailability();
                }
            });
            return;
        }

        if (!_explicitClose && _session.Machine.State == JobState.AwaitingTeacherReview)
        {
            _session.Cancel();
            _explicitClose = true;
            DialogResult = DialogResult.Cancel;
        }

        base.OnFormClosing(e);
    }

    private void SelectionChanged()
    {
        if (_refreshingNodes)
        {
            return;
        }

        var selectedIndex = SelectedIndex() ?? -1;
        if (_editorDirty && selectedIndex != _loadedIndex)
        {
            // Enabled=false prevents an interactive selection change. This
            // guard also makes programmatic/UIA changes atomic: the dirty
            // paragraph remains selected until Apply commits it.
            _refreshingNodes = true;
            _nodeList.SelectedIndex = _loadedIndex;
            _refreshingNodes = false;
            return;
        }

        LoadSelection(selectedIndex);
    }

    private void LoadSelection(int index)
    {
        _loadedIndex = index;
        var node = index >= 0 && index < _session.Draft.Revision.Document.Nodes.Count
            ? _session.Draft.Revision.Document.Nodes[index]
            : null;

        _loadingEditor = true;
        _editor.ReadOnly = node is not Paragraph;
        _editor.Text = node switch
        {
            Paragraph paragraph => paragraph.Text,
            null => string.Empty,
            _ => ExactContents(node),
        };
        _loadingEditor = false;
        _editorDirty = false;
        UpdateActionAvailability();
    }

    private void EditorTextChanged()
    {
        if (_loadingEditor)
        {
            return;
        }

        _editorDirty = _loadedIndex >= 0
            && _loadedIndex < _session.Draft.Revision.Document.Nodes.Count
            && _session.Draft.Revision.Document.Nodes[_loadedIndex] is Paragraph paragraph
            && !string.Equals(_editor.Text, paragraph.Text, StringComparison.Ordinal);
        _previewReadiness.BeginLoad();
        if (!_editorDirty)
        {
            // Returning the field to the exact current draft does not revive a
            // previously displayed DOM. Load and verify a fresh generation.
            RefreshComparisonAndPreview();
        }

        UpdateActionAvailability();
    }

    private void ApplyEdit()
    {
        if (_editorDirty
            && _loadedIndex >= 0
            && _loadedIndex < _session.Draft.Revision.Document.Nodes.Count
            && _session.Draft.Revision.Document.Nodes[_loadedIndex] is Paragraph)
        {
            var replacement = new Paragraph(_editor.Text);
            _session.ReplaceNode(_loadedIndex, replacement);
            _editorDirty = false;
            Refresh(_loadedIndex);
        }
    }

    private void EditSelectedElement()
    {
        if (_editorDirty || SelectedIndex() is not int index)
        {
            return;
        }

        var expectedRevision = _session.Draft.Revision;
        var selected = expectedRevision.Document.Nodes[index];
        using var editor = new NodeEditorForm(selected);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is null)
        {
            return;
        }

        _session.ReplaceNode(index, expectedRevision, editor.Result);
        Refresh(index);
    }

    private void MoveSelection(int delta)
    {
        if (!_editorDirty && SelectedIndex() is int index)
        {
            var target = index + delta;
            if (target >= 0 && target < _session.Draft.Revision.Document.Nodes.Count)
            {
                _session.MoveNode(index, target);
                Refresh(target);
            }
        }
    }

    private void WithSelection(Action<int> action)
    {
        if (!_editorDirty && SelectedIndex() is int index)
        {
            action(index);
            Refresh(Math.Min(index, _session.Draft.Revision.Document.Nodes.Count - 1));
        }
    }

    private void ApproveAndClose()
    {
        if (_editorDirty
            || !_session.CanApprove
            || !_previewReadiness.IsReadyFor(
                _session.Draft.Revision,
                _session.ViewContext.PreviewRequest))
        {
            return;
        }

        Result = _session.Approve(Environment.UserName, DateTimeOffset.UtcNow);
        _explicitClose = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RejectAndClose()
    {
        _session.Reject();
        _explicitClose = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void AcknowledgementChanged()
    {
        if (_refreshingAcknowledgement)
        {
            return;
        }

        _session.SetRequiredIssuesAcknowledged(_acknowledgeWarnings.Checked);
        UpdateActionAvailability();
    }

    private int? SelectedIndex() => _nodeList.SelectedIndex >= 0 ? _nodeList.SelectedIndex : null;

    private void Refresh(int selectIndex)
    {
        _refreshingNodes = true;
        _nodeList.BeginUpdate();
        _nodeList.Items.Clear();
        foreach (var node in _session.Draft.Revision.Document.Nodes)
        {
            _nodeList.Items.Add(Describe(node));
        }

        _nodeList.EndUpdate();
        if (_nodeList.Items.Count > 0)
        {
            _nodeList.SelectedIndex = Math.Clamp(selectIndex, 0, _nodeList.Items.Count - 1);
        }
        else
        {
            _nodeList.SelectedIndex = -1;
        }

        _refreshingNodes = false;
        LoadSelection(_nodeList.SelectedIndex);

        _issueList.BeginUpdate();
        _issueList.Items.Clear();
        foreach (var issue in _session.Issues)
        {
            _issueList.Items.Add(UiStrings.Format(
                UiStrings.IssueLine,
                DisplaySeverity(issue.Severity),
                issue.Message));
        }

        _issueList.EndUpdate();
        _issueList.SelectedIndex = _issueList.Items.Count > 0 ? 0 : -1;
        LoadIssueDetail();

        _refreshingAcknowledgement = true;
        _session.SetRequiredIssuesAcknowledged(acknowledged: false);
        _acknowledgeWarnings.Checked = false;
        _acknowledgeWarnings.Visible = _session.RequiredAcknowledgements.Count > 0;
        _acknowledgeWarnings.Enabled = _acknowledgeWarnings.Visible;
        _refreshingAcknowledgement = false;

        RefreshComparisonAndPreview();
        UpdateActionAvailability();
    }

    private void RefreshComparisonAndPreview()
    {
        var source = _session.ViewContext.Source;
        _sourceContext.Text = source is null
            ? UiStrings.SourceUnavailable
            : string.Join(
                Environment.NewLine,
                source.Description,
                UiStrings.Format(UiStrings.ExactSourceCodeUnitCount, source.ExactText.Length),
                UiStrings.ExactSourceBegins,
                source.ExactText,
                UiStrings.ExactSourceEnds);
        _currentDraft.Text = ExactArtifactDocumentText.Describe(
            _session.Draft.Revision.Document);

        var request = _session.ViewContext.PreviewRequest;
        _previewProfile.Text = UiStrings.Format(
            UiStrings.UnapprovedPreviewProfile,
            DisplayPreviewTarget(request.Target),
            DisplayAudience(request.Audience),
            request.TextScalePercent.ToString("0.###", CultureInfo.InvariantCulture),
            request.TargetLanguageFirst
                ? UiStrings.PreviewTargetLanguageFirst
                : UiStrings.PreviewSourceLanguageFirst);
        _previewProfile.AccessibleName = _previewProfile.Text;

        var generation = _previewReadiness.BeginLoad();
        SetPreviewStatus(UiStrings.UnapprovedPreviewLoading);
        try
        {
            var preview = UnapprovedDraftPreviewFactory.CreateForBrowser(
                _session.Draft,
                request,
                generation);
            if (!ReferenceEquals(preview.Revision, _session.Draft.Revision)
                || !Equals(preview.Request, request)
                || string.IsNullOrWhiteSpace(preview.LoadMarker))
            {
                throw new InvalidOperationException();
            }

            _previewReadiness.Expect(
                generation,
                preview.Revision,
                preview.Request,
                preview.LoadMarker);
            _previewBrowser.DocumentText = preview.DocumentText;
        }
        catch (Exception failure) when (failure is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ExternalException)
        {
            _previewReadiness.Fail(generation);
            _previewBrowser.DocumentText = string.Empty;
            SetPreviewStatus(UiStrings.Format(UiStrings.PreviewUnavailable, failure.Message));
        }
    }

    private void PreviewBrowserNavigating(object? sender, WebBrowserNavigatingEventArgs eventArgs)
    {
        _previewReadiness.NavigationStarted();
        SetPreviewStatus(UiStrings.UnapprovedPreviewLoading);
        if (eventArgs.Url is { Scheme: not "about" })
        {
            eventArgs.Cancel = true;
        }

        UpdateActionAvailability();
    }

    private void PreviewBrowserDocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs eventArgs)
    {
        string? marker = null;
        try
        {
            if (eventArgs.Url is { Scheme: "about" }
                && _previewBrowser.ReadyState == WebBrowserReadyState.Complete)
            {
                marker = _previewBrowser.Document?
                    .GetElementById(UnapprovedDraftPreviewFactory.LoadMarkerElementId)?
                    .GetAttribute("content");
            }
        }
        catch (Exception failure) when (failure is InvalidOperationException
            or ObjectDisposedException
            or ExternalException)
        {
            // Browser/COM failures are not approval evidence. A later exact
            // completion may recover; until then readiness remains revoked.
            marker = null;
        }

        var ready = _previewReadiness.ObserveDocumentCompleted(
            _session.Draft.Revision,
            _session.ViewContext.PreviewRequest,
            marker);
        SetPreviewStatus(ready
            ? UiStrings.UnapprovedPreviewStatus
            : UiStrings.UnapprovedPreviewLoading);
        UpdateActionAvailability();
    }

    private void SetPreviewStatus(string status)
    {
        _previewStatus.Text = status;
        _previewStatus.AccessibleName = status;
    }

    private void UpdateActionAvailability()
    {
        var hasSelection = SelectedIndex() is not null;
        _nodeList.Enabled = !_editorDirty;
        _applyEdit.Enabled = _editorDirty;
        _editElement.Enabled = !_editorDirty && hasSelection;
        _remove.Enabled = !_editorDirty && hasSelection;
        _moveUp.Enabled = !_editorDirty && SelectedIndex() > 0;
        _moveDown.Enabled = !_editorDirty
            && SelectedIndex() is int index
            && index < _session.Draft.Revision.Document.Nodes.Count - 1;
        _approve.Enabled = !_editorDirty
            && _session.CanApprove
            && _previewReadiness.IsReadyFor(
                _session.Draft.Revision,
                _session.ViewContext.PreviewRequest);
        _editStatus.Text = _editorDirty ? UiStrings.PendingEditMustBeAppliedOrRejected : string.Empty;
        _editStatus.Visible = _editorDirty;
    }

    private void LoadIssueDetail()
    {
        _issueDetail.Text = _issueList.SelectedIndex >= 0
            && _issueList.SelectedIndex < _session.Issues.Count
                ? UiStrings.Format(
                    UiStrings.IssueLine,
                    DisplaySeverity(_session.Issues[_issueList.SelectedIndex].Severity),
                    _session.Issues[_issueList.SelectedIndex].Message)
                : string.Empty;
    }

    private static string Describe(DocumentNode node) => node switch
    {
        Heading heading => UiStrings.Format(UiStrings.NodeHeading, heading.Level, heading.Text),
        Paragraph paragraph => UiStrings.Format(UiStrings.NodeParagraph, paragraph.Text),
        OrderedSteps steps => UiStrings.Format(UiStrings.NodeSteps, steps.Steps.Count),
        UnorderedList list => UiStrings.Format(UiStrings.NodeList, list.Items.Count),
        TableNode table => UiStrings.Format(UiStrings.NodeTable, table.Rows.Count),
        Card card => UiStrings.Format(UiStrings.NodeCard, card.Title),
        ImageReference image => UiStrings.Format(UiStrings.NodeImage, image.AltText),
        BilingualPair pair => UiStrings.Format(UiStrings.NodeBilingual, pair.SourceText),
        ChoiceSet choices => UiStrings.Format(UiStrings.NodeChoices, choices.Options.Count),
        EvidenceLink evidence => UiStrings.Format(UiStrings.NodeEvidence, evidence.Claim),
        Citation citation => UiStrings.Format(UiStrings.NodeCitation, citation.Text),
        TeacherOnlyNotice notice => UiStrings.Format(UiStrings.NodeTeacherOnly, notice.Text),
        StepRow => UiStrings.NodeStepRow,
        PageBreak => UiStrings.NodePageBreak,
        VectorGraphic graphic => UiStrings.Format(UiStrings.NodeVectorGraphic, graphic.Description),
        _ => node.GetType().Name,
    };

    private static string ExactContents(DocumentNode node)
    {
        var lines = new List<string>();
        switch (node)
        {
            case Heading heading:
                lines.Add(UiStrings.Format(UiStrings.NodeHeading, heading.Level, heading.Text));
                break;

            case OrderedSteps steps:
                lines.Add(UiStrings.Format(UiStrings.NodeSteps, steps.Steps.Count));
                for (var index = 0; index < steps.Steps.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeOrderedStepItem, index + 1, steps.Steps[index]));
                }

                break;

            case UnorderedList list:
                lines.Add(UiStrings.Format(UiStrings.NodeList, list.Items.Count));
                for (var index = 0; index < list.Items.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeListItem, index + 1, list.Items[index]));
                }

                break;

            case TableNode table:
                lines.Add(UiStrings.Format(UiStrings.NodeTable, table.Rows.Count));
                if (table.HeaderRow is null)
                {
                    lines.Add(UiStrings.NodeTableNoHeaders);
                }
                else
                {
                    for (var column = 0; column < table.HeaderRow.Count; column++)
                    {
                        lines.Add(UiStrings.Format(UiStrings.NodeTableHeaderCell, column + 1, table.HeaderRow[column]));
                    }
                }

                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var column = 0; column < table.Rows[row].Count; column++)
                    {
                        lines.Add(UiStrings.Format(UiStrings.NodeTableCell, row + 1, column + 1, table.Rows[row][column]));
                    }
                }

                break;

            case Card card:
                lines.Add(UiStrings.Format(UiStrings.NodeCard, card.Title));
                lines.Add(UiStrings.Format(UiStrings.NodeBodyContent, card.Body));
                break;

            case ImageReference image:
                lines.Add(UiStrings.Format(UiStrings.NodeImage, image.AltText));
                lines.Add(UiStrings.Format(UiStrings.NodeImageAssetIdentity, image.Asset.Value));
                break;

            case BilingualPair pair:
                lines.Add(UiStrings.Format(UiStrings.NodeTextContent, pair.SourceText));
                lines.Add(UiStrings.Format(UiStrings.NodeTranslationContent, pair.TargetText));
                lines.Add(UiStrings.Format(UiStrings.NodeLocalesContent, pair.SourceLocale, pair.TargetLocale));
                break;

            case ChoiceSet choices:
                lines.Add(UiStrings.Format(UiStrings.NodeChoices, choices.Options.Count));
                for (var index = 0; index < choices.Options.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeChoiceItem, index + 1, choices.Options[index]));
                }

                break;

            case EvidenceLink evidence:
                lines.Add(UiStrings.Format(UiStrings.NodeEvidence, evidence.Claim));
                lines.Add(UiStrings.Format(UiStrings.NodeSourcePointerContent, evidence.SourcePointer));
                break;

            case Citation citation:
                lines.Add(UiStrings.Format(UiStrings.NodeCitation, citation.Text));
                break;

            case TeacherOnlyNotice notice:
                lines.Add(UiStrings.Format(UiStrings.NodeTeacherOnly, notice.Text));
                break;

            case StepRow step:
                lines.Add(UiStrings.NodeStepRow);
                lines.Add(UiStrings.Format(UiStrings.NodeTextContent, step.Text));
                lines.Add(UiStrings.Format(UiStrings.NodeTranslationContent, step.TargetText));
                lines.Add(UiStrings.Format(UiStrings.NodeLocalesContent, step.SourceLocale, step.TargetLocale));
                lines.Add(UiStrings.Format(UiStrings.NodeImageAssetIdentity, step.Symbol?.Asset.Value));
                lines.Add(UiStrings.Format(UiStrings.NodeSymbolAltContent, step.Symbol?.AltText));
                break;

            case PageBreak:
                lines.Add(UiStrings.NodePageBreak);
                break;

            case VectorGraphic graphic:
                AddVectorContents(lines, graphic);
                break;

            default:
                // The domain validator blocks unknown node types; naming the
                // runtime type here avoids presenting an empty review pane.
                lines.Add(node.GetType().Name);
                break;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddVectorContents(List<string> lines, VectorGraphic graphic)
    {
        lines.Add(UiStrings.Format(UiStrings.NodeVectorGraphic, graphic.Description));
        lines.Add(UiStrings.Format(
            UiStrings.NodeDimensionsContent,
            ExactNumber(graphic.WidthMm),
            ExactNumber(graphic.HeightMm)));
        lines.Add(UiStrings.Format(
            UiStrings.NodeVectorPrimitiveCounts,
            graphic.Primitives.OfType<LineSeg>().Count(),
            graphic.Primitives.OfType<CircleShape>().Count(),
            graphic.Primitives.OfType<RectShape>().Count(),
            graphic.Primitives.OfType<TextLabel>().Count()));

        foreach (var primitive in graphic.Primitives)
        {
            lines.Add(primitive switch
            {
                LineSeg line => UiStrings.Format(
                    UiStrings.NodeVectorLineDetail,
                    ExactNumber(line.X1),
                    ExactNumber(line.Y1),
                    ExactNumber(line.X2),
                    ExactNumber(line.Y2),
                    ExactNumber(line.StrokeWidthMm),
                    DisplayBoolean(line.Dashed)),
                CircleShape circle => UiStrings.Format(
                    UiStrings.NodeVectorCircleDetail,
                    ExactNumber(circle.CenterX),
                    ExactNumber(circle.CenterY),
                    ExactNumber(circle.RadiusMm),
                    ExactNumber(circle.StrokeWidthMm),
                    DisplayBoolean(circle.Filled)),
                RectShape rectangle => UiStrings.Format(
                    UiStrings.NodeVectorRectangleDetail,
                    ExactNumber(rectangle.X),
                    ExactNumber(rectangle.Y),
                    ExactNumber(rectangle.WidthMm),
                    ExactNumber(rectangle.HeightMm),
                    ExactNumber(rectangle.StrokeWidthMm),
                    DisplayBoolean(rectangle.Filled)),
                TextLabel label => UiStrings.Format(
                    UiStrings.NodeVectorTextLabelDetail,
                    ExactNumber(label.X),
                    ExactNumber(label.Y),
                    label.Text,
                    ExactNumber(label.FontSizeMm),
                    DisplayAnchor(label.Anchor)),
                _ => primitive.GetType().Name,
            });
        }
    }

    private static string ExactNumber(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static string DisplayBoolean(bool value)
        => value ? UiStrings.BooleanYes : UiStrings.BooleanNo;

    private static string DisplayAnchor(TextAnchor anchor)
        => anchor switch
        {
            TextAnchor.Start => UiStrings.TextAnchorStart,
            TextAnchor.Middle => UiStrings.TextAnchorMiddle,
            TextAnchor.End => UiStrings.TextAnchorEnd,
            _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
        };

    private static string DisplaySeverity(ValidationSeverity severity)
        => severity switch
        {
            ValidationSeverity.Info => UiStrings.SeverityInformation,
            ValidationSeverity.Warning => UiStrings.SeverityWarning,
            ValidationSeverity.Blocking => UiStrings.SeverityBlocking,
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };

    private static string DisplayPreviewTarget(RenderTarget target)
        => target switch
        {
            RenderTarget.PrintHtml => UiStrings.PreviewPrintLayout,
            RenderTarget.AccessibleHtml => UiStrings.PreviewAccessibleLayout,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

    private static string DisplayAudience(RenderAudience audience)
        => audience switch
        {
            RenderAudience.Teacher => UiStrings.AudienceTeacher,
            RenderAudience.Learner => UiStrings.AudienceLearner,
            _ => throw new ArgumentOutOfRangeException(nameof(audience)),
        };
}
