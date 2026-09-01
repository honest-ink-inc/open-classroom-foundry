// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>
/// Attaches wrapping-caption behavior to a standard WinForms check box without
/// replacing its framework accessibility peer.
/// </summary>
internal static class ReflowingCheckBox
{
    internal static CheckBox Attach(
        CheckBox checkBox,
        int minimumWidth = 0,
        int minimumHeight = 1)
    {
        ArgumentNullException.ThrowIfNull(checkBox);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumHeight);
        if (checkBox.GetType() != typeof(CheckBox))
        {
            throw new ArgumentException(null, nameof(checkBox));
        }

        checkBox.AutoSize = false;
        checkBox.MinimumSize = new Size(minimumWidth, minimumHeight);

        var behavior = new CheckBoxBehavior(
            checkBox,
            new Size(minimumWidth, minimumHeight));
        behavior.Attach();
        return checkBox;
    }

    internal static int RequiredCaptionHeight(CheckBox checkBox)
    {
        ArgumentNullException.ThrowIfNull(checkBox);
        var textWidth = Math.Max(
            1,
            checkBox.ClientSize.Width
                - checkBox.Padding.Horizontal
                - SystemInformation.MenuCheckSize.Width
                - 3);
        return TextRenderer.MeasureText(
            UiStrings.WithoutMnemonic(checkBox.Text),
            checkBox.Font,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak |
            TextFormatFlags.TextBoxControl |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix).Height + checkBox.Padding.Vertical;
    }

    private sealed class CheckBoxBehavior(CheckBox checkBox, Size minimumSize)
    {
        private Size _minimumSize = minimumSize;
        private Size _lastAppliedMinimum = minimumSize;
        private bool _reflowing;

        internal void Attach()
        {
            checkBox.FontChanged += ReflowPropertyChanged;
            checkBox.Layout += ReflowLayout;
            checkBox.PaddingChanged += ReflowPropertyChanged;
            checkBox.SizeChanged += ReflowPropertyChanged;
            checkBox.TextChanged += ReflowPropertyChanged;
            checkBox.Disposed += CheckBoxDisposed;
            Reflow();
        }

        private void ReflowPropertyChanged(object? sender, EventArgs e)
            => Reflow();

        private void ReflowLayout(object? sender, LayoutEventArgs e)
            => Reflow();

        private void Reflow()
        {
            if (_reflowing || checkBox.IsDisposed || checkBox.ClientSize.Width <= 0)
            {
                return;
            }

            _reflowing = true;
            try
            {
                if (checkBox.MinimumSize != _lastAppliedMinimum)
                {
                    _minimumSize = checkBox.MinimumSize;
                }

                var desiredHeight = Math.Max(
                    _minimumSize.Height,
                    RequiredCaptionHeight(checkBox));
                var desiredMinimum = new Size(_minimumSize.Width, desiredHeight);
                if (checkBox.MinimumSize != desiredMinimum)
                {
                    _lastAppliedMinimum = desiredMinimum;
                    checkBox.MinimumSize = desiredMinimum;
                }
                else
                {
                    _lastAppliedMinimum = checkBox.MinimumSize;
                }

                if (checkBox.Dock == DockStyle.None && checkBox.Height != desiredHeight)
                {
                    checkBox.Height = desiredHeight;
                }
            }
            finally
            {
                _reflowing = false;
            }
        }

        private void CheckBoxDisposed(object? sender, EventArgs e)
        {
            checkBox.FontChanged -= ReflowPropertyChanged;
            checkBox.Layout -= ReflowLayout;
            checkBox.PaddingChanged -= ReflowPropertyChanged;
            checkBox.SizeChanged -= ReflowPropertyChanged;
            checkBox.TextChanged -= ReflowPropertyChanged;
            checkBox.Disposed -= CheckBoxDisposed;
        }
    }
}
