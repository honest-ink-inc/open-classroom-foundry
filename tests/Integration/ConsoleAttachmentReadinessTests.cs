// SPDX-License-Identifier: GPL-3.0-or-later

namespace Foundry.Tests.Integration;

public sealed class ConsoleAttachmentReadinessTests
{
    [Fact]
    public void A_missing_target_console_is_retried_inside_the_supplied_budget()
    {
        var attempted = 0;
        var observed = 0;
        var delayed = 0;
        var elapsedMilliseconds = 0L;

        var result = ConsoleAttachmentReadiness.Wait(
            () =>
            {
                attempted++;
                elapsedMilliseconds++;
                return attempted < 3
                    ? new ConsoleAttachmentAttempt(false, NativeMethods.ErrorInvalidHandle)
                    : new ConsoleAttachmentAttempt(true, 0);
            },
            () =>
            {
                observed++;
                return RunningTarget();
            },
            () => elapsedMilliseconds,
            () =>
            {
                delayed++;
                elapsedMilliseconds += 2;
            },
            timeoutMilliseconds: 15);

        Assert.Equal(ConsoleAttachmentOutcome.Attached, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(2, observed);
        Assert.Equal(2, delayed);
        Assert.Equal(0, result.LastAttachError);
        Assert.True(result.AttachedToTarget);
        Assert.Equal(7, result.ElapsedMilliseconds);
    }

    [Fact]
    public void A_nontransient_attach_error_fails_after_confirming_the_target_is_running()
    {
        var observed = 0;
        var delayed = false;

        var result = ConsoleAttachmentReadiness.Wait(
            () => new ConsoleAttachmentAttempt(false, NativeMethods.ErrorAccessDenied),
            () =>
            {
                observed++;
                return RunningTarget();
            },
            () => 0,
            () => delayed = true,
            timeoutMilliseconds: 15);

        Assert.Equal(ConsoleAttachmentOutcome.AttachFailed, result.Outcome);
        Assert.Equal(NativeMethods.ErrorAccessDenied, result.LastAttachError);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, observed);
        Assert.False(delayed);
    }

    [Fact]
    public void The_existing_budget_bounds_missing_console_observation()
    {
        var elapsedMilliseconds = 0L;
        var delayed = 0;

        var result = ConsoleAttachmentReadiness.Wait(
            () => new ConsoleAttachmentAttempt(false, NativeMethods.ErrorInvalidHandle),
            RunningTarget,
            () => elapsedMilliseconds,
            () =>
            {
                delayed++;
                elapsedMilliseconds += 2;
            },
            timeoutMilliseconds: 4);

        Assert.Equal(ConsoleAttachmentOutcome.TimedOut, result.Outcome);
        Assert.Equal(NativeMethods.ErrorInvalidHandle, result.LastAttachError);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, delayed);
        Assert.Equal(NativeMethods.WaitTimeout, result.LastTargetObservation.WaitResult);
        Assert.Equal(4, result.ElapsedMilliseconds);
    }

    [Fact]
    public void The_supplied_clock_already_represents_the_shared_budget()
    {
        var elapsedMilliseconds = 14L;

        var result = ConsoleAttachmentReadiness.Wait(
            () => new ConsoleAttachmentAttempt(false, NativeMethods.ErrorInvalidHandle),
            RunningTarget,
            () => elapsedMilliseconds,
            () => elapsedMilliseconds += 2,
            timeoutMilliseconds: 15);

        Assert.Equal(ConsoleAttachmentOutcome.TimedOut, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(16, result.ElapsedMilliseconds);
    }

    [Fact]
    public void The_deadline_rechecks_target_state_before_classifying_a_timeout()
    {
        var elapsedMilliseconds = 0L;
        var observations = 0;

        var result = ConsoleAttachmentReadiness.Wait(
            () => new ConsoleAttachmentAttempt(false, NativeMethods.ErrorInvalidHandle),
            () =>
            {
                observations++;
                return observations == 1
                    ? RunningTarget()
                    : new ConsoleTargetObservation(NativeMethods.WaitObject0, 0);
            },
            () => elapsedMilliseconds,
            () => elapsedMilliseconds = 2,
            timeoutMilliseconds: 2);

        Assert.Equal(ConsoleAttachmentOutcome.TargetExited, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(2, observations);
        Assert.Equal(NativeMethods.WaitObject0, result.LastTargetObservation.WaitResult);
        Assert.Equal(2, result.ElapsedMilliseconds);
    }

    [Fact]
    public void An_attachment_completed_at_the_deadline_is_rejected_but_retained_for_detachment()
    {
        var elapsedMilliseconds = 14L;

        var result = ConsoleAttachmentReadiness.Wait(
            () =>
            {
                elapsedMilliseconds = 15;
                return new ConsoleAttachmentAttempt(true, 0);
            },
            RunningTarget,
            () => elapsedMilliseconds,
            () => throw new InvalidOperationException("A late attachment must not enter the retry delay."),
            timeoutMilliseconds: 15);

        Assert.Equal(ConsoleAttachmentOutcome.TimedOut, result.Outcome);
        Assert.True(result.AttachedToTarget);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(15, result.ElapsedMilliseconds);
    }

    [Theory]
    [InlineData(NativeMethods.WaitObject0, 0, (int)ConsoleAttachmentOutcome.TargetExited)]
    [InlineData(NativeMethods.WaitFailed, 123, (int)ConsoleAttachmentOutcome.TargetWatchFailed)]
    public void Target_state_stops_the_attachment_wait(
        uint targetWait,
        int targetWaitError,
        int expectedOutcome)
    {
        var delayed = false;

        var result = ConsoleAttachmentReadiness.Wait(
            () => new ConsoleAttachmentAttempt(false, NativeMethods.ErrorInvalidHandle),
            () => new ConsoleTargetObservation(targetWait, targetWaitError),
            () => 0,
            () => delayed = true,
            timeoutMilliseconds: 15);

        Assert.Equal((ConsoleAttachmentOutcome)expectedOutcome, result.Outcome);
        Assert.Equal(NativeMethods.ErrorInvalidHandle, result.LastAttachError);
        Assert.Equal(targetWait, result.LastTargetObservation.WaitResult);
        Assert.Equal(targetWaitError, result.LastTargetObservation.ErrorCode);
        Assert.Equal(1, result.Attempts);
        Assert.False(delayed);
    }

    private static ConsoleTargetObservation RunningTarget() =>
        new(NativeMethods.WaitTimeout, 0);
}
