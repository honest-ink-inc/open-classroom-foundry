// SPDX-License-Identifier: GPL-3.0-or-later
// Headed windows and STA form fixtures must not race each other: one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// Marks a test that must drive real windows through UI Automation. It runs by
/// default — on the pilot machine and on CI runners that offer an interactive
/// desktop — and is skipped VISIBLY (never silently passed) where no desktop
/// exists or the operator sets OCF_SKIP_HEADED=1. A skipped headed test is an
/// honest gap in the evidence, and the results say so.
/// </summary>
public sealed class HeadedFactAttribute : FactAttribute
{
    public HeadedFactAttribute()
    {
        if (!Environment.UserInteractive)
        {
            Skip = "No interactive desktop: headed UIA evidence not collected in this run.";
        }
        else if (Environment.GetEnvironmentVariable("OCF_SKIP_HEADED") == "1")
        {
            Skip = "OCF_SKIP_HEADED=1: headed UIA evidence not collected in this run.";
        }
    }
}

/// <summary>WinForms wants an STA thread; xunit supplies MTA. Bridge, join, rethrow.</summary>
public static class Sta
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
