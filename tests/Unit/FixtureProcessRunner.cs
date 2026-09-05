// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text;

namespace Foundry.Tests.Unit;

// This adapter belongs only to the PowerShell contract-test fixture. Its fake
// implementations measure injected outcomes, not native process-tree behavior.
internal interface IFixtureProcess : IDisposable
{
    bool Start();
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }
    int ExitCode { get; }
    bool WaitForExit(int milliseconds);
    void KillEntireProcessTree();
}

internal sealed class NativeFixtureProcess(ProcessStartInfo startInfo) : IFixtureProcess
{
    private readonly Process process = new() { StartInfo = startInfo };

    public TextReader StandardOutput => process.StandardOutput;
    public TextReader StandardError => process.StandardError;
    public int ExitCode => process.ExitCode;
    internal int? StartedProcessId { get; private set; }

    public bool Start()
    {
        var started = process.Start();
        if (started)
        {
            StartedProcessId = process.Id;
        }

        return started;
    }
    public bool WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);
    public void KillEntireProcessTree() => process.Kill(entireProcessTree: true);
    public void Dispose() => process.Dispose();
}

internal sealed record FixtureProcessLimits(
    int WorkMilliseconds = 30_000,
    int CleanupMilliseconds = 2_000,
    int DrainMilliseconds = 2_000,
    int SettlementMilliseconds = 500,
    int CaptureCharacters = 1_048_576);

internal sealed record FixtureStreamSnapshot(string Text, string State, string? Failure, bool Truncated);

internal interface IFixtureSettlementClock
{
    long ElapsedMilliseconds { get; }
}

internal sealed class StopwatchFixtureSettlementClock : IFixtureSettlementClock
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    public long ElapsedMilliseconds => clock.ElapsedMilliseconds;
}

internal enum FixtureDisposalStage { NotRequired, Deferred, Queued, Entered, CallbackExited }

[Flags]
internal enum FixtureDisposalDeferral
{
    None = 0,
    RootExitUnobserved = 1,
    CleanupUnsettled = 2,
    CaptureUnsettled = 4,
    SettlementBudgetExhausted = 8,
}

internal sealed record FixtureDisposalObservation(
    FixtureDisposalStage Stage,
    FixtureDisposalDeferral DeferredReasons,
    int SettlementBudgetMilliseconds,
    long DecisionElapsedMilliseconds,
    int RemainingAtDecisionMilliseconds,
    long? WaitElapsedMilliseconds,
    int? RemainingAtWaitMilliseconds,
    long? CallbackEntryElapsedMilliseconds,
    long? CallbackExitElapsedMilliseconds,
    long SnapshotElapsedMilliseconds,
    bool TaskCompletionObserved,
    bool TaskFaultObserved,
    bool? WaitReturnedSettled)
{
    internal string Describe() =>
        $"Stage: {Stage}; DeferredReasons: {DeferredReasons}; SharedBudgetMs: {SettlementBudgetMilliseconds}; " +
        $"DecisionElapsedMs: {DecisionElapsedMilliseconds}; RemainingAtDecisionMs: {RemainingAtDecisionMilliseconds}; " +
        $"WaitElapsedMs: {Observed(WaitElapsedMilliseconds)}; RemainingAtWaitMs: {Observed(RemainingAtWaitMilliseconds)}; " +
        $"CallbackEntryElapsedMs: {Observed(CallbackEntryElapsedMilliseconds)}; " +
        $"CallbackExitElapsedMs: {Observed(CallbackExitElapsedMilliseconds)}; SnapshotElapsedMs: {SnapshotElapsedMilliseconds}; " +
        $"TaskCompletionAtSnapshot: {TaskCompletionObserved}; TaskFaultAtSnapshot: {TaskFaultObserved}; " +
        $"WaitReturnedSettled: {WaitReturnedSettled?.ToString() ?? "NotObserved"}; TimelySettlement: NotEstablishedByObservations";

    private static string Observed(long? milliseconds) =>
        milliseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NotObserved";
}

internal sealed record FixtureProcessResult(
    int? ExitCode,
    FixtureStreamSnapshot Output,
    FixtureStreamSnapshot Error,
    string? PrimaryFailure,
    IReadOnlyList<string> SecondaryOutcomes,
    bool RootExitObserved,
    bool CleanupSettled,
    bool CaptureSettled,
    bool DisposalSettled,
    bool SafeToStartAnotherFixture)
{
    internal string StandardOutput => Output.Text;
    internal string StandardError => Error.Text;
    // Test controls may release their synthetic operations and await this exact
    // aggregate. Completion does not settle deferred disposal or prove tree exit.
    internal Task StartedOperations { get; init; } = Task.CompletedTask;
    internal FixtureDisposalObservation? DisposalObservation { get; init; }

    internal string Describe() =>
        $"Primary: {PrimaryFailure ?? "None"}{Environment.NewLine}" +
        $"NativeExit: {ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unavailable"}; " +
        $"RootExitObserved: {RootExitObserved}; DescendantExit: NotEstablished{Environment.NewLine}" +
        $"CleanupSettled: {CleanupSettled}; CaptureSettled: {CaptureSettled}; " +
        $"DisposalSettled: {DisposalSettled}; SafeToStartAnotherFixture: {SafeToStartAnotherFixture}{Environment.NewLine}" +
        $"DisposalObservation: {DisposalObservation?.Describe() ?? "Unavailable"}{Environment.NewLine}" +
        $"Secondary outcomes:{Environment.NewLine}{string.Join(Environment.NewLine, SecondaryOutcomes)}{Environment.NewLine}" +
        $"--- stdout ({Output.State}; truncated={Output.Truncated}) ---{Environment.NewLine}" +
        $"{Output.Text}{Environment.NewLine}{Output.Failure}{Environment.NewLine}" +
        $"--- stderr ({Error.State}; truncated={Error.Truncated}) ---{Environment.NewLine}" +
        $"{Error.Text}{Environment.NewLine}{Error.Failure}";
}

internal sealed class FixtureProcessException(FixtureProcessResult result) : Exception(result.Describe())
{
    internal FixtureProcessResult Result { get; } = result;
}

internal sealed class FixtureProcessRunner
{
    private readonly Lock runGate = new();
    private readonly FixtureProcessLimits limits;
    private readonly Func<Action, Task> scheduleDisposal;
    private readonly Func<IFixtureSettlementClock> startSettlementClock;
    private FixtureProcessResult? unsafePriorResult;

    // Keep ownership of an unsettled adapter. Timed waits do not cancel Kill,
    // Dispose, or a reader which ignores cancellation, and do not make them safe.
    private IFixtureProcess? unsettledProcess;

    internal FixtureProcessRunner(
        FixtureProcessLimits? requestedLimits = null,
        Func<Action, Task>? disposalScheduler = null,
        Func<IFixtureSettlementClock>? settlementClockFactory = null)
    {
        limits = requestedLimits ?? new();
        if (limits.WorkMilliseconds is <= 0 or > 30_000
            || limits.CleanupMilliseconds is <= 0 or > 2_000
            || limits.DrainMilliseconds is <= 0 or > 2_000
            || limits.SettlementMilliseconds is <= 0 or > 500
            || limits.CaptureCharacters is <= 0 or > 1_048_576)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLimits));
        }

        // Trusted, process-free controls may inject only these two boundaries.
        // Normal disposal scheduling and the shared monotonic clock are unchanged.
        scheduleDisposal = disposalScheduler ?? (static action => Task.Run(action));
        startSettlementClock = settlementClockFactory ?? (static () => new StopwatchFixtureSettlementClock());
    }

    internal FixtureProcessResult Run(Func<IFixtureProcess> createProcess)
    {
        lock (runGate)
        {
            if (unsafePriorResult is not null)
            {
                GC.KeepAlive(unsettledProcess);
                throw new InvalidOperationException(
                    "A prior fixture has uncertain cleanup/capture; no new process was created. " +
                    "A fresh test host is required after ownership is resolved." + Environment.NewLine +
                    unsafePriorResult.Describe());
            }

            return RunOwned(createProcess);
        }
    }

    private FixtureProcessResult RunOwned(Func<IFixtureProcess> createProcess)
    {
        IFixtureProcess? process = null;
        StreamCapture? output = null;
        StreamCapture? error = null;
        string? primary = null;
        var secondary = new List<string>();
        var started = false;
        var startUncertain = false;
        var rootExited = false;
        int? exitCode = null;
        Task<CleanupOutcome>? cleanup = null;
        var cleanupSettled = true;
        var cleanupWithinLimit = true;

        // Start and the native bounded WaitForExit remain synchronous OS API
        // boundaries. This helper does not promise to interrupt a stalled OS call.
        try
        {
            process = createProcess();
            started = process.Start();
            if (!started)
            {
                primary = "StartupFailure: the process API returned false.";
            }
        }
        catch (Exception failure)
        {
            primary = "StartupFailure: " + failure;
            startUncertain = true;
        }

        if (started)
        {
            output = new StreamCapture(() => process!.StandardOutput, limits.CaptureCharacters);
            error = new StreamCapture(() => process!.StandardError, limits.CaptureCharacters);
            try
            {
                rootExited = process!.WaitForExit(limits.WorkMilliseconds);
                if (!rootExited)
                {
                    primary = $"WorkTimeout: the PowerShell evidence-fixture process exceeded " +
                        $"{limits.WorkMilliseconds} ms (the normal work budget remains 30 seconds).";
                }
            }
            catch (Exception failure)
            {
                primary = "ExitObservationFailure: " + failure;
            }

            if (!rootExited)
            {
                cleanup = Task.Run(() => RequestCleanup(process!));
                cleanupWithinLimit = WaitSettled(cleanup, limits.CleanupMilliseconds);
                cleanupSettled = cleanup.IsCompleted;
                if (!cleanupWithinLimit)
                {
                    secondary.Add("CleanupDeadline: the owned cleanup operation is unsettled; cancellation is not asserted.");
                }
            }
        }

        var captures = Task.WhenAll(output?.Completion ?? Task.CompletedTask, error?.Completion ?? Task.CompletedTask);
        var drained = WaitSettled(captures, limits.DrainMilliseconds);
        if (!drained)
        {
            primary ??= "StreamDrainTimeout: redirected streams did not finish within the separate drain budget.";
            secondary.Add("DrainDeadline: output below is a partial snapshot, not claimed EOF.");
        }

        // CancelAsync avoids running a reader's cancellation callback on this
        // thread. Settlement has its own shared clock; pending operations remain
        // pending even after the caller has stopped waiting for them.
        var settlementClock = startSettlementClock();
        var outputCancellation = output?.CancelAsync() ?? Task.CompletedTask;
        var errorCancellation = error?.CancelAsync() ?? Task.CompletedTask;
        var cancellation = Task.WhenAll(outputCancellation, errorCancellation);
        var settlement = Task.WhenAll(captures, cancellation);
        var settlementWithinLimit = WaitSettled(settlement, Remaining(settlementClock, limits.SettlementMilliseconds));
        var captureSettled = captures.IsCompleted && cancellation.IsCompleted;
        if (!settlementWithinLimit)
        {
            primary ??= "CaptureSettlementTimeout: the separate settlement budget expired.";
            secondary.Add("CaptureSettlementDeadline: later task completion does not undo the missed settlement budget.");
        }
        if (!captureSettled)
        {
            primary ??= "StreamCaptureUnsettled: cancellation did not settle every capture operation.";
            secondary.Add("CaptureSettlement: at least one read or cancellation operation remains unsettled.");
        }
        if (cancellation.IsFaulted)
        {
            primary ??= "CaptureCancellationFailure: a cancellation callback failed.";
            secondary.Add("CaptureCancellationFailure: " + cancellation.Exception);
        }

        cleanupSettled = cleanup?.IsCompleted ?? true;
        var cleanupSucceeded = cleanup is null;

        if (cleanup is not null && cleanup.IsCompletedSuccessfully)
        {
            // Result access is only on an already completed task, never a wait.
            var outcome = cleanup.Result;
            rootExited = outcome.RootExitObserved;
            cleanupSucceeded = outcome.KillRequestReturned && rootExited;
            secondary.AddRange(outcome.Outcomes);
            cleanupSettled = true;
        }
        else if (cleanup is not null && cleanup.IsFaulted)
        {
            secondary.Add("CleanupFailure: " + cleanup.Exception);
        }

        if (rootExited)
        {
            try
            {
                exitCode = process!.ExitCode;
            }
            catch (Exception failure)
            {
                primary ??= "ExitCodeUnavailable: the root exit code could not be read.";
                secondary.Add("ExitCodeFailure: " + failure);
            }
        }

        var outputSnapshot = output?.Snapshot() ?? new FixtureStreamSnapshot(string.Empty, "Unavailable", null, false);
        var errorSnapshot = error?.Snapshot() ?? new FixtureStreamSnapshot(string.Empty, "Unavailable", null, false);
        foreach (var (name, snapshot) in new[] { ("stdout", outputSnapshot), ("stderr", errorSnapshot) })
        {
            if (snapshot.Failure is not null || snapshot.Truncated)
            {
                primary ??= "StreamCaptureFailure: one or more redirected streams are incomplete.";
                secondary.Add($"{name} capture: {snapshot.State}; truncated={snapshot.Truncated}; {snapshot.Failure}");
            }
        }

        var disposalSettled = process is null;
        var disposalSucceeded = process is null;
        Task? disposal = null;
        long? waitElapsed = null;
        int? remainingAtWait = null;
        bool? waitReturnedSettled = null;
        var deferredReasons = FixtureDisposalDeferral.None;
        var callbackProgress = new DisposalCallbackProgress(null, null);
        var decisionElapsed = settlementClock.ElapsedMilliseconds;
        var remainingAtDecision = Remaining(decisionElapsed, limits.SettlementMilliseconds);
        if (process is not null && (!started || rootExited) && cleanupSettled && captureSettled
            && remainingAtDecision > 0)
        {
            disposal = scheduleDisposal(() =>
            {
                var entry = new DisposalCallbackProgress(settlementClock.ElapsedMilliseconds, null);
                Volatile.Write(ref callbackProgress, entry);
                try
                {
                    try { process.Dispose(); }
                    finally
                    {
                        output?.Dispose();
                        error?.Dispose();
                    }
                }
                finally
                {
                    // Callback exit, including a fault, is not task completion or
                    // proof that the shared budget was met.
                    Volatile.Write(ref callbackProgress,
                        entry with { ExitElapsedMilliseconds = settlementClock.ElapsedMilliseconds });
                }
            });
            waitElapsed = settlementClock.ElapsedMilliseconds;
            remainingAtWait = Remaining(waitElapsed.Value, limits.SettlementMilliseconds);
            var disposalWithinLimit = WaitSettled(disposal, remainingAtWait.Value);
            waitReturnedSettled = disposalWithinLimit;
            disposalSettled = disposal.IsCompleted;
            disposalSucceeded = disposalWithinLimit && disposal.IsCompletedSuccessfully;
            if (!disposalSucceeded)
            {
                primary ??= "DisposalFailure: fixture ownership did not settle cleanly.";
                secondary.Add(disposal.IsFaulted
                    ? "DisposalFailure: " + disposal.Exception
                    : disposalSettled
                        ? "DisposalDeadline: completion was observed after the wait limit; timely settlement is unproven."
                        : "DisposalDeadline: the owned disposal operation remains unsettled.");
            }
        }
        else if (process is not null)
        {
            if (started && !rootExited)
            {
                deferredReasons |= FixtureDisposalDeferral.RootExitUnobserved;
            }

            if (!cleanupSettled)
            {
                deferredReasons |= FixtureDisposalDeferral.CleanupUnsettled;
            }

            if (!captureSettled)
            {
                deferredReasons |= FixtureDisposalDeferral.CaptureUnsettled;
            }

            if (remainingAtDecision <= 0)
            {
                deferredReasons |= FixtureDisposalDeferral.SettlementBudgetExhausted;
            }

            secondary.Add("DisposalDeferred: ownership is retained because a root or operation is unresolved, or no settlement budget remains.");
        }

        var safe = !startUncertain && (!started || rootExited)
            && cleanupSettled && cleanupWithinLimit && cleanupSucceeded
            && captureSettled && settlementWithinLimit && cancellation.IsCompletedSuccessfully && drained
            && disposalSucceeded
            && (!started || (outputSnapshot.State == "Eof" && errorSnapshot.State == "Eof"
                && !outputSnapshot.Truncated && !errorSnapshot.Truncated));
        if (!safe)
        {
            primary ??= "OwnershipUncertain: no further fixture may start through this runner.";
        }

        // Observe task completion first, then acquire the atomically published
        // entry/exit pair. Reading the pair first could combine an old absent
        // entry with a task that completed between those reads. Later callback
        // progress cannot mutate the value copied into this result or its verdict.
        var taskCompletionObserved = disposal?.IsCompleted ?? false;
        var taskFaultObserved = taskCompletionObserved && disposal!.IsFaulted;
        var callbackSnapshot = Volatile.Read(ref callbackProgress);
        var stage = process is null ? FixtureDisposalStage.NotRequired
            : disposal is null ? FixtureDisposalStage.Deferred
            : callbackSnapshot.ExitElapsedMilliseconds is not null ? FixtureDisposalStage.CallbackExited
            : callbackSnapshot.EntryElapsedMilliseconds is not null ? FixtureDisposalStage.Entered
            : FixtureDisposalStage.Queued;
        var disposalObservation = new FixtureDisposalObservation(
            stage, deferredReasons, limits.SettlementMilliseconds, decisionElapsed, remainingAtDecision,
            waitElapsed, remainingAtWait, callbackSnapshot.EntryElapsedMilliseconds,
            callbackSnapshot.ExitElapsedMilliseconds, settlementClock.ElapsedMilliseconds,
            taskCompletionObserved, taskFaultObserved, waitReturnedSettled);
        var result = new FixtureProcessResult(
            exitCode, outputSnapshot, errorSnapshot, primary, [.. secondary],
            rootExited, cleanupSettled, captureSettled, disposalSettled, safe)
        {
            DisposalObservation = disposalObservation,
            StartedOperations = Task.WhenAll(
                captures, cancellation, (Task?)cleanup ?? Task.CompletedTask, disposal ?? Task.CompletedTask),
        };
        if (!safe)
        {
            unsafePriorResult = result;
            unsettledProcess = process;
        }

        if (primary is not null)
        {
            throw new FixtureProcessException(result);
        }

        return result;
    }

    private CleanupOutcome RequestCleanup(IFixtureProcess process)
    {
        var clock = Stopwatch.StartNew();
        var outcomes = new List<string>();
        var killReturned = false;
        try
        {
            process.KillEntireProcessTree();
            killReturned = true;
            outcomes.Add("TreeKillRequest: returned; this does not establish descendant exit.");
        }
        catch (Exception failure)
        {
            outcomes.Add("TreeKillFailure: " + failure);
        }

        var rootExited = false;
        var remaining = Remaining(clock, limits.CleanupMilliseconds);
        if (remaining > 0)
        {
            try
            {
                rootExited = process.WaitForExit(remaining);
            }
            catch (Exception failure)
            {
                outcomes.Add("CleanupRootWaitFailure: " + failure);
            }
        }

        if (!rootExited)
        {
            outcomes.Add("RootExitUnobserved: cleanup is unsafe; no descendant-completeness claim is made.");
        }

        return new CleanupOutcome(rootExited, killReturned, [.. outcomes]);
    }

    private static int Remaining(Stopwatch clock, int budget) =>
        (int)Math.Max(0, budget - clock.ElapsedMilliseconds);

    private static int Remaining(IFixtureSettlementClock clock, int budget) =>
        Remaining(clock.ElapsedMilliseconds, budget);

    private static int Remaining(long elapsedMilliseconds, int budget) =>
        (int)Math.Max(0, budget - elapsedMilliseconds);

    private static bool WaitSettled(Task task, int milliseconds)
    {
        if (task.IsCompleted)
        {
            return true;
        }

        if (milliseconds <= 0)
        {
            return false;
        }

        try
        {
            return task.Wait(milliseconds);
        }
        catch (AggregateException)
        {
            // Faulted is settled, not successful. Callers retain the fault.
            return true;
        }
    }

    private sealed record CleanupOutcome(bool RootExitObserved, bool KillRequestReturned, IReadOnlyList<string> Outcomes);

    private sealed record DisposalCallbackProgress(long? EntryElapsedMilliseconds, long? ExitElapsedMilliseconds);

    private sealed class StreamCapture : IDisposable
    {
        private readonly Lock gate = new();
        private readonly StringBuilder text = new();
        private readonly CancellationTokenSource cancellation = new();
        private string state = "Pending";
        private string? failure;
        private bool truncated;

        internal StreamCapture(Func<TextReader> openReader, int maximumCharacters)
        {
            Completion = Task.Run(async () =>
            {
                try
                {
                    var reader = openReader();
                    var buffer = new char[4096];
                    while (true)
                    {
                        var count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false);
                        lock (gate)
                        {
                            if (count == 0)
                            {
                                state = "Eof";
                                return;
                            }

                            var retained = Math.Min(count, maximumCharacters - text.Length);
                            text.Append(buffer, 0, retained);
                            truncated |= retained != count;
                        }
                    }
                }
                catch (Exception exception)
                {
                    lock (gate)
                    {
                        state = exception is OperationCanceledException ? "Canceled" : "Faulted";
                        failure = exception.ToString();
                    }
                }
            });
        }

        internal Task Completion { get; }
        internal Task CancelAsync() => cancellation.CancelAsync();
        public void Dispose() => cancellation.Dispose();

        internal FixtureStreamSnapshot Snapshot()
        {
            lock (gate)
            {
                return new FixtureStreamSnapshot(text.ToString(), state, failure, truncated);
            }
        }
    }
}
