// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

// Headed windows and STA form fixtures must not race each other: one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// Owns the bounded shutdown contract for real headed child processes. Sending
/// a kill request is not evidence that the child has exited; every caller must
/// observe termination before the next headed fixture may start.
/// </summary>
public static class HeadedProcessLifetime
{
    public static void TerminateAndWait(Process process, int timeoutMs = 5000)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The child exited between the state check and the kill request.
            }
        }

        if (!process.WaitForExit(timeoutMs))
        {
            throw new TimeoutException(
                $"Headed child process {process.Id} did not exit within {timeoutMs} ms after shutdown was requested.");
        }
    }
}

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
