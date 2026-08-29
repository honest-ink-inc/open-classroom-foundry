// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// Gate B review surface — prototype. Standard controls only (ADR-002): every
/// behavior lives in <see cref="ReviewSession"/>; this form only binds it.
/// Pending before any pilot: assistive-technology walkthrough (NVDA/Narrator),
/// source-versus-draft split view, and uncertainty highlighting from OCR.
/// </summary>
public sealed class ReviewForm : Form
{
    private readonly ReviewSession _session;
    private readonly ListBox _nodeList;
    private readonly TextBox _editor;
    private readonly ListBox _issueList;
    private readonly Button _applyEdit;
    private readonly Button _remove;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly Button _approve;
    private readonly Button _reject;

    public ReviewForm(ReviewSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));

        Text = $"{ProductIdentity.PublicName} — review before anything prints";
        MinimumSize = new Size(720, 480);

        _nodeList = new ListBox { Dock = DockStyle.Fill, AccessibleName = "Draft elements" };
        _nodeList.SelectedIndexChanged += (_, _) => LoadSelection();

        _editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AccessibleName = "Selected element text",
        };

        _issueList = new ListBox { Dock = DockStyle.Fill, AccessibleName = "Validation issues" };

        _applyEdit = MakeButton("&Apply edit", (_, _) => ApplyEdit());
        _remove = MakeButton("&Remove element", (_, _) => WithSelection(_session.RemoveNode));
        _moveUp = MakeButton("Move &up", (_, _) => MoveSelection(-1));
        _moveDown = MakeButton("Move &down", (_, _) => MoveSelection(+1));
        _approve = MakeButton("A&pprove", (_, _) => ApproveAndClose());
        _reject = MakeButton("Re&ject", (_, _) => RejectAndClose());

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([_applyEdit, _remove, _moveUp, _moveDown, _approve, _reject]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        split.Panel1.Controls.Add(_nodeList);

        var rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        rightSplit.Panel1.Controls.Add(_editor);
        rightSplit.Panel2.Controls.Add(_issueList);
        split.Panel2.Controls.Add(rightSplit);

        Controls.Add(split);
        Controls.Add(buttons);

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

    private void LoadSelection()
    {
        _editor.Text = SelectedIndex() is int index && _session.Draft.Revision.Document.Nodes[index] is Paragraph paragraph
            ? paragraph.Text
            : string.Empty;
    }

    private void ApplyEdit()
    {
        if (SelectedIndex() is int index && _session.Draft.Revision.Document.Nodes[index] is Paragraph)
        {
            _session.ReplaceNode(index, new Paragraph(_editor.Text));
            Refresh(index);
        }
    }

    private void MoveSelection(int delta)
    {
        if (SelectedIndex() is int index)
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
        if (SelectedIndex() is int index)
        {
            action(index);
            Refresh(Math.Min(index, _session.Draft.Revision.Document.Nodes.Count - 1));
        }
    }

    private void ApproveAndClose()
    {
        Result = _session.Approve(Environment.UserName, DateTimeOffset.UtcNow);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RejectAndClose()
    {
        _session.Reject();
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private int? SelectedIndex() => _nodeList.SelectedIndex >= 0 ? _nodeList.SelectedIndex : null;

    private void Refresh(int selectIndex)
    {
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

        _issueList.BeginUpdate();
        _issueList.Items.Clear();
        foreach (var issue in _session.Issues)
        {
            _issueList.Items.Add($"{issue.Severity}: {issue.Message}");
        }

        _issueList.EndUpdate();

        _approve.Enabled = _session.CanApprove;
    }

    private static string Describe(DocumentNode node) => node switch
    {
        Heading heading => $"Heading {heading.Level}: {heading.Text}",
        Paragraph paragraph => $"Paragraph: {paragraph.Text}",
        OrderedSteps steps => $"Steps ({steps.Steps.Count})",
        UnorderedList list => $"List ({list.Items.Count})",
        TableNode table => $"Table ({table.Rows.Count} rows)",
        Card card => $"Card: {card.Title}",
        ImageReference image => $"Image: {image.AltText}",
        BilingualPair pair => $"Bilingual: {pair.SourceText}",
        ChoiceSet choices => $"Choices ({choices.Options.Count})",
        EvidenceLink evidence => $"Evidence: {evidence.Claim}",
        Citation citation => $"Citation: {citation.Text}",
        TeacherOnlyNotice notice => $"Teacher-only: {notice.Text}",
        _ => node.GetType().Name,
    };
}
