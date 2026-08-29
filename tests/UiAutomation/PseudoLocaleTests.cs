// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Foundry.App.WinForms;

namespace Foundry.Tests.UiAutomation;

// The pseudo-locale smoke pass (forge item 4): under "ẋẋ" every chrome string
// stretches at least forty percent, is bracketed so truncation confesses, and
// the whole window mirrors right-to-left — so the multilingual seat's week-3
// hour is spent on real language, not on layout defects a machine can catch.
// This assembly runs serialized, so flipping the static locale mode is safe;
// every test restores Neutral in a finally block.

public class PseudoLocaleTests
{
    private static void InPseudo(Action assert)
    {
        UiLocale.Set(UiLocaleMode.Pseudo);
        try
        {
            assert();
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Neutral_is_the_default_and_returns_the_exact_english_catalog()
    {
        Assert.Equal(UiLocaleMode.Neutral, UiLocale.Mode);
        Assert.Equal("&Apply edit", UiStrings.ApplyEdit);
        Assert.Equal("en", UiLocale.LanguageTag);
    }

    [Fact]
    public void Pseudo_strings_stretch_forty_percent_bracket_the_ends_and_keep_mnemonics()
        => InPseudo(() =>
        {
            Assert.Equal("ẋẋ", UiLocale.LanguageTag);

            foreach (var (neutral, pseudo) in new[]
            {
                ("&Apply edit", UiStrings.ApplyEdit),
                ("Move &down", UiStrings.MoveDown),
                ("Draft elements", UiStrings.DraftElements),
                ("I saw something concerning — &pause here", UiStrings.SafetyPause),
            })
            {
                Assert.StartsWith("⟦", pseudo, StringComparison.Ordinal);
                Assert.EndsWith("⟧", pseudo, StringComparison.Ordinal);
                Assert.True(pseudo.Length >= neutral.Length * 1.4,
                    $"'{pseudo}' is not 40% longer than '{neutral}'");
            }

            // The mnemonic character survives untransformed: Alt+key still works.
            Assert.Contains("&A", UiStrings.ApplyEdit, StringComparison.Ordinal);
            Assert.Contains("&d", UiStrings.MoveDown, StringComparison.Ordinal);
        });

    [Fact]
    public void Pseudo_transformation_is_deterministic_and_keeps_format_placeholders()
        => InPseudo(() =>
        {
            Assert.Equal(UiStrings.ApproveDescription, UiStrings.ApproveDescription);

            Assert.Contains("{0}", UiStrings.StatusLaneConfirmed, StringComparison.Ordinal);
            Assert.Contains("{1}", UiStrings.NodeHeading, StringComparison.Ordinal);

            // Formatting a pseudo template must not throw and must embed the value.
            Assert.Contains("Amber", UiStrings.Format(UiStrings.StatusLaneConfirmed, "Amber"), StringComparison.Ordinal);
        });

    [Fact]
    public void The_product_name_never_localizes_but_the_phrase_beside_it_does()
        => InPseudo(() =>
        {
            Assert.StartsWith(ProductIdentity.PublicName, UiStrings.ReviewWindowTitle, StringComparison.Ordinal);
            Assert.Contains("⟦", UiStrings.ReviewWindowTitle, StringComparison.Ordinal);
        });

    [Fact]
    public void Both_surfaces_smoke_render_in_pseudo_with_mirrored_chrome_and_no_bare_string()
        => InPseudo(() => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();

            foreach (var form in new Form[] { review, capture })
            {
                form.Show();

                // The renderer's forced-RTL discipline, wired into the chrome.
                Assert.Equal(RightToLeft.Yes, form.RightToLeft);
                Assert.True(form.RightToLeftLayout, "Pseudo-locale must mirror the window layout");

                // Every focusable control speaks the pseudo catalog — a bare
                // (unbracketed) name is a string that escaped localization.
                foreach (var control in ReviewSurfaceContractTests.Flatten(form)
                    .Where(c => c.TabStop && c.CanSelect))
                {
                    Assert.Contains("⟦", control.AccessibilityObject.Name, StringComparison.Ordinal);
                }
            }
        }));

    [HeadedFact]
    public void The_pseudo_locale_switch_reaches_the_real_chrome_end_to_end()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Foundry.App.WinForms.exe");
        using var process = Process.Start(new ProcessStartInfo(exe, $"{UiaHarness.Switch} review {UiLocale.PseudoSwitch}"))!;
        try
        {
            AutomationElement? window = null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 20000 && window is null)
            {
                Thread.Sleep(200);
                window = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
            }

            Assert.NotNull(window);
            Assert.StartsWith(ProductIdentity.PublicName, window.Current.Name, StringComparison.Ordinal);
            Assert.Contains("⟦", window.Current.Name, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
