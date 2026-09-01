// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>
/// Attaches wrapping status-line behavior to a standard WinForms label without
/// replacing its framework accessibility peer.
/// </summary>
internal static class ReflowingStatusLabel
{
    internal static Label Attach(
        Label label,
        int minimumHeight,
        DockStyle dock = DockStyle.Bottom)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumHeight);
        if (label.GetType() != typeof(Label))
        {
            throw new ArgumentException(null, nameof(label));
        }

        if (dock is not (DockStyle.Top or DockStyle.Bottom))
        {
            throw new ArgumentOutOfRangeException(nameof(dock));
        }

        label.AutoSize = false;
        label.Dock = dock;
        label.MinimumSize = new Size(0, minimumHeight);
        label.Height = minimumHeight;
        label.UseMnemonic = false;

        var behavior = new StatusLabelBehavior(label);
        behavior.Attach();
        return label;
    }

    private sealed class StatusLabelBehavior(Label label)
    {
        private bool _reflowing;

        internal void Attach()
        {
            label.FontChanged += ReflowPropertyChanged;
            label.PaddingChanged += ReflowPropertyChanged;
            label.SizeChanged += ReflowPropertyChanged;
            label.TextChanged += ReflowPropertyChanged;
            label.Disposed += LabelDisposed;
            Reflow();
        }

        private void ReflowPropertyChanged(object? sender, EventArgs e)
            => Reflow();

        private void Reflow()
        {
            if (_reflowing || label.IsDisposed || label.ClientSize.Width <= 0)
            {
                return;
            }

            _reflowing = true;
            try
            {
                var preferredHeight = label.GetPreferredSize(
                    new Size(label.ClientSize.Width, 0)).Height;
                label.Height = Math.Max(label.MinimumSize.Height, preferredHeight);
            }
            finally
            {
                _reflowing = false;
            }
        }

        private void LabelDisposed(object? sender, EventArgs e)
        {
            label.FontChanged -= ReflowPropertyChanged;
            label.PaddingChanged -= ReflowPropertyChanged;
            label.SizeChanged -= ReflowPropertyChanged;
            label.TextChanged -= ReflowPropertyChanged;
            label.Disposed -= LabelDisposed;
        }
    }
}
