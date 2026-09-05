// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Foundry.Tests.Unit;

public sealed class FixtureProcessRunnerTests(ITestOutputHelper output)
{
    private static readonly FixtureProcessLimits ControlLimits = new(
        CleanupMilliseconds: 50,
        DrainMilliseconds: 50,
        SettlementMilliseconds: 250);

    [Fact]
    public void Timeout_retains_available_standard_output()
    {
        var failure = RunFailure(new SyntheticProcess { WorkExited = false });

        Assert.Contains("synthetic-standard-output", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Timeout_retains_available_standard_error()
    {
        var failure = RunFailure(new SyntheticProcess { WorkExited = false });

        Assert.Contains("synthetic-standard-error", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Timeout_cleanup_failure_does_not_replace_primary_timeout()
    {
        var failure = RunFailure(new SyntheticProcess
        {
            WorkExited = false,
            KillFailure = new InvalidOperationException("synthetic-kill-failure"),
        });

        Assert.Contains("Primary: WorkTimeout", failure.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic-kill-failure", failure.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic-standard-output", failure.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic-standard-error", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Faulted_stdout_retains_the_separate_stderr_and_capture_failure()
    {
        var failure = RunFailure(new SyntheticProcess
        {
            Output = new FaultedReader(),
        });

        Assert.Contains("synthetic-standard-error", failure.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic-stdout-read-failure", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_failure_identifies_the_start_phase()
    {
        var failure = RunFailure(new SyntheticProcess
        {
            StartFailure = new InvalidOperationException("synthetic-start-failure"),
        });

        Assert.Contains("Primary: StartupFailure", failure.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic-start-failure", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Injected_nonzero_exit_retains_exit_code_and_separate_streams()
    {
        var result = new FixtureProcessRunner().Run(() => new SyntheticProcess { ExitCode = 23 });

        Assert.Equal(23, result.ExitCode);
        Assert.Equal("synthetic-standard-output", result.StandardOutput);
        Assert.Equal("synthetic-standard-error", result.StandardError);
        Assert.True(result.RootExitObserved);
        Assert.True(result.SafeToStartAnotherFixture);
        Assert.Contains("DescendantExit: NotEstablished", result.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Original_work_budget_remains_thirty_seconds_and_cleanup_uses_only_remaining_budget()
    {
        var process = new SyntheticProcess { WorkExited = false };
        var result = RunFailure(process).Result;

        Assert.Equal(30_000, process.WaitBudgets.First());
        Assert.InRange(process.WaitBudgets.Last(), 1, 2_000);
        Assert.True(result.CleanupSettled);
        Assert.True(result.RootExitObserved);
        Assert.True(result.SafeToStartAnotherFixture);
    }

    [Fact]
    public void A_work_budget_above_the_original_limit_is_rejected_before_creation()
    {
        var created = false;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var runner = new FixtureProcessRunner(new FixtureProcessLimits(WorkMilliseconds: 30_001));
            runner.Run(() =>
            {
                created = true;
                return new SyntheticProcess();
            });
        });
        Assert.False(created);
    }

    [Fact]
    public void Capture_fault_preserves_its_partial_prefix_and_the_other_stream()
    {
        var result = RunFailure(new SyntheticProcess { Output = new FaultedReader() }).Result;

        Assert.Equal("synthetic-partial-output", result.StandardOutput);
        Assert.Equal("Faulted", result.Output.State);
        Assert.Equal("synthetic-standard-error", result.StandardError);
        Assert.True(result.CaptureSettled);
        Assert.False(result.SafeToStartAnotherFixture);
    }

    [Fact]
    public void Failure_to_open_one_reader_does_not_discard_the_other_reader()
    {
        var result = RunFailure(new SyntheticProcess
        {
            OutputOpenFailure = new IOException("synthetic-open-output-failure"),
        }).Result;

        Assert.Equal("Faulted", result.Output.State);
        Assert.Contains("synthetic-open-output-failure", result.Output.Failure, StringComparison.Ordinal);
        Assert.Equal("synthetic-standard-error", result.StandardError);
    }

    [Fact]
    public Task Root_exit_does_not_stand_in_for_stream_eof_or_descendant_exit() =>
        VerifyRootExitDoesNotAdmitCapture(holdCancellationCompletion: false);

    [Fact]
    public Task Root_exit_does_not_admit_a_capture_with_explicitly_held_cancellation_completion() =>
        VerifyRootExitDoesNotAdmitCapture(holdCancellationCompletion: true);

    private async Task VerifyRootExitDoesNotAdmitCapture(bool holdCancellationCompletion)
    {
        var reader = new PendingReader(ignoreCancellation: false, holdCancellationCompletion);
        var process = new SyntheticProcess { Output = reader, BeforeWorkWait = reader.EnsureReading };
        var runner = new FixtureProcessRunner(ControlLimits);
        FixtureProcessResult? result = null;
        try
        {
            result = RunFailure(process, runner).Result;

            Assert.True(result.RootExitObserved);
            Assert.Equal("synthetic-pending-prefix", result.StandardOutput);
            Assert.True(result.Output.State is "Pending" or "Canceled", result.Describe());
            if (result.CaptureSettled)
            {
                Assert.Equal("Canceled", result.Output.State);
            }
            else
            {
                Assert.False(result.DisposalSettled);
                Assert.Contains(result.SecondaryOutcomes, item => item.StartsWith("CaptureSettlement:", StringComparison.Ordinal));
                Assert.Contains(result.SecondaryOutcomes, item => item.StartsWith("DisposalDeferred:", StringComparison.Ordinal));
            }

            if (holdCancellationCompletion)
            {
                Assert.False(result.CaptureSettled);
                Assert.Equal("Pending", result.Output.State);
                Assert.False(result.StartedOperations.IsCompleted);
            }

            Assert.False(result.SafeToStartAnotherFixture);
            Assert.StartsWith("StreamDrainTimeout", result.PrimaryFailure, StringComparison.Ordinal);
            Assert.Contains(result.SecondaryOutcomes, item => item.StartsWith("DrainDeadline:", StringComparison.Ordinal));
            Assert.Contains("DescendantExit: NotEstablished", result.Describe(), StringComparison.Ordinal);
            AssertRefusesNextCreation(runner);
        }
        finally
        {
            reader.Release();
            if (result is not null)
            {
                await result.StartedOperations.WaitAsync(TimeSpan.FromSeconds(2));
                output.WriteLine("Complete synthetic started-operation set settled after explicit reader release; prior unsafe result remains unchanged.");
            }
        }

        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public async Task A_reader_ignoring_cancellation_is_unsettled_and_blocks_the_next_fixture()
    {
        var reader = new PendingReader(ignoreCancellation: true);
        var process = new SyntheticProcess { Output = reader, BeforeWorkWait = reader.EnsureReading };
        var runner = new FixtureProcessRunner(ControlLimits);
        FixtureProcessResult? result = null;
        try
        {
            result = RunFailure(process, runner).Result;

            Assert.Equal("synthetic-pending-prefix", result.StandardOutput);
            Assert.Equal("Pending", result.Output.State);
            Assert.False(result.CaptureSettled);
            Assert.False(result.DisposalSettled);
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.False(process.Disposed);
            AssertRefusesNextCreation(runner);
        }
        finally
        {
            reader.Release();
            if (result is not null)
            {
                await result.StartedOperations.WaitAsync(TimeSpan.FromSeconds(2));
                output.WriteLine("Complete synthetic started-operation set settled after explicit reader release; prior unsafe result remains unchanged.");
            }
        }
    }

    [Fact]
    public async Task An_uncancellable_kill_is_not_relabelled_settled_after_the_wait_budget()
    {
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var process = new SyntheticProcess
        {
            WorkExited = false,
            KillAction = () =>
            {
                try { release.Wait(2_000); }
                finally { finished.Set(); }
            },
        };
        var runner = new FixtureProcessRunner(ControlLimits);
        FixtureProcessResult? result = null;
        try
        {
            result = RunFailure(process, runner).Result;

            Assert.StartsWith("WorkTimeout", result.PrimaryFailure, StringComparison.Ordinal);
            Assert.False(result.CleanupSettled);
            Assert.False(result.RootExitObserved);
            Assert.False(result.DisposalSettled);
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.Contains(result.SecondaryOutcomes, item => item.StartsWith("CleanupDeadline", StringComparison.Ordinal));
            AssertRefusesNextCreation(runner);
        }
        finally
        {
            release.Set();
            if (result is not null)
            {
                await result.StartedOperations.WaitAsync(TimeSpan.FromSeconds(2));
                output.WriteLine("Complete synthetic started-operation set settled after explicit kill release; prior unsafe result remains unchanged.");
            }
        }
    }

    [Fact]
    public async Task An_unsettled_disposal_remains_unsafe_and_its_worker_is_explicitly_released()
    {
        using var release = new ManualResetEventSlim();
        var process = new SyntheticProcess { DisposeAction = () => release.Wait(2_000) };
        var runner = new FixtureProcessRunner(ControlLimits);
        FixtureProcessResult? result = null;
        try
        {
            result = RunFailure(process, runner).Result;

            Assert.False(result.DisposalSettled);
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.StartsWith("DisposalFailure", result.PrimaryFailure, StringComparison.Ordinal);
            AssertRefusesNextCreation(runner);
        }
        finally
        {
            release.Set();
            if (result is not null)
            {
                await result.StartedOperations.WaitAsync(TimeSpan.FromSeconds(2));
                output.WriteLine("Complete synthetic disposal worker settled after explicit release; prior unsafe result remains unchanged.");
            }
        }
    }

    [Fact]
    public void A_failed_tree_kill_does_not_become_success_because_the_root_exits()
    {
        var result = RunFailure(new SyntheticProcess
        {
            WorkExited = false,
            CleanupExited = true,
            KillFailure = new IOException("synthetic-tree-kill-failure-before-root-exit"),
        }).Result;

        Assert.True(result.RootExitObserved);
        Assert.True(result.CleanupSettled);
        Assert.True(result.CaptureSettled);
        Assert.False(result.SafeToStartAnotherFixture);
    }

    [Fact]
    public void Native_childless_timeout_retains_owned_pid_exit_and_separate_streams_at_thirty_seconds()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        // Synthetic and childless. The root also expires on its own; the control
        // does not rely on spawning descendants or changing the 30-second cap.
        startInfo.ArgumentList.Add(
            "[Console]::Out.WriteLine('synthetic-native-stdout;pid=' + $PID); " +
            "[Console]::Error.WriteLine('synthetic-native-stderr'); " +
            "[Console]::Out.Flush(); [Console]::Error.Flush(); " +
            "[Threading.Thread]::Sleep(35000); exit 91");
        var process = new NativeFixtureProcess(startInfo);
        var clock = Stopwatch.StartNew();
        var exception = Assert.Throws<FixtureProcessException>(() => new FixtureProcessRunner().Run(() => process));
        clock.Stop();
        output.WriteLine($"Native childless control; owned PID={process.StartedProcessId}; elapsed ms={clock.ElapsedMilliseconds}:");
        output.WriteLine(exception.ToString());
        var result = exception.Result;

        Assert.StartsWith("WorkTimeout", result.PrimaryFailure, StringComparison.Ordinal);
        Assert.NotNull(process.StartedProcessId);
        Assert.Contains($"synthetic-native-stdout;pid={process.StartedProcessId}", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("synthetic-native-stderr", result.StandardError, StringComparison.Ordinal);
        Assert.NotNull(result.ExitCode);
        Assert.True(result.RootExitObserved);
        Assert.True(result.CleanupSettled);
        Assert.True(result.CaptureSettled);
        Assert.True(result.DisposalSettled);
        Assert.True(result.SafeToStartAnotherFixture);
        Assert.True(result.StartedOperations.IsCompletedSuccessfully);
        Assert.Contains("DescendantExit: NotEstablished", result.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cleanup_root_wait_failure_is_secondary_to_the_work_timeout()
    {
        var result = RunFailure(new SyntheticProcess
        {
            WorkExited = false,
            CleanupWaitFailure = new IOException("synthetic-cleanup-wait-failure"),
        }).Result;

        Assert.StartsWith("WorkTimeout", result.PrimaryFailure, StringComparison.Ordinal);
        Assert.Contains(result.SecondaryOutcomes, item => item.Contains("synthetic-cleanup-wait-failure", StringComparison.Ordinal));
        Assert.True(result.CleanupSettled);
        Assert.False(result.RootExitObserved);
        Assert.False(result.SafeToStartAnotherFixture);
    }

    [Fact]
    public void Failed_disposal_cannot_replace_timeout_or_discard_streams()
    {
        var result = RunFailure(new SyntheticProcess
        {
            WorkExited = false,
            DisposeFailure = new IOException("synthetic-dispose-failure"),
        }).Result;

        Assert.StartsWith("WorkTimeout", result.PrimaryFailure, StringComparison.Ordinal);
        Assert.Contains(result.SecondaryOutcomes, item => item.Contains("synthetic-dispose-failure", StringComparison.Ordinal));
        Assert.Equal("synthetic-standard-output", result.StandardOutput);
        Assert.Equal("synthetic-standard-error", result.StandardError);
        Assert.True(result.DisposalSettled);
        Assert.False(result.SafeToStartAnotherFixture);
    }

    [Fact]
    public void Start_returning_false_records_unavailable_streams_without_a_root_wait()
    {
        var process = new SyntheticProcess { StartReturned = false };
        var result = RunFailure(process).Result;

        Assert.StartsWith("StartupFailure", result.PrimaryFailure, StringComparison.Ordinal);
        Assert.Empty(process.WaitBudgets);
        Assert.Equal("Unavailable", result.Output.State);
        Assert.Equal("Unavailable", result.Error.State);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public void Factory_failure_has_a_primary_record_and_refuses_future_creation()
    {
        var runner = new FixtureProcessRunner();
        var exception = Assert.Throws<FixtureProcessException>(() => runner.Run(
            () => throw new IOException("synthetic-factory-failure")));

        output.WriteLine(exception.ToString());
        Assert.Contains("synthetic-factory-failure", exception.Message, StringComparison.Ordinal);
        Assert.StartsWith("StartupFailure", exception.Result.PrimaryFailure, StringComparison.Ordinal);
        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public void Bounded_capture_discloses_truncation_and_does_not_claim_complete_output()
    {
        var result = RunFailure(new SyntheticProcess(), new FixtureProcessRunner(
            new FixtureProcessLimits(CaptureCharacters: 8))).Result;

        Assert.Equal("syntheti", result.StandardOutput);
        Assert.Equal("syntheti", result.StandardError);
        Assert.True(result.Output.Truncated);
        Assert.True(result.Error.Truncated);
        Assert.Equal("Eof", result.Output.State);
        Assert.False(result.SafeToStartAnotherFixture);
    }

    private static void AssertRefusesNextCreation(FixtureProcessRunner runner)
    {
        var created = false;
        var failure = Assert.Throws<InvalidOperationException>(() => runner.Run(() =>
        {
            created = true;
            return new SyntheticProcess();
        }));
        Assert.False(created);
        Assert.Contains("no new process was created", failure.Message, StringComparison.Ordinal);
    }

    private FixtureProcessException RunFailure(SyntheticProcess process, FixtureProcessRunner? runner = null)
    {
        var failure = Assert.Throws<FixtureProcessException>(() => (runner ?? new FixtureProcessRunner()).Run(() => process));
        output.WriteLine("Injected adapter failure (not a native process observation):");
        output.WriteLine(failure.ToString());
        return failure;
    }

    private sealed class SyntheticProcess : IFixtureProcess
    {
        public TextReader Output { get; init; } = new StringReader("synthetic-standard-output");
        public TextReader StandardOutput => OutputOpenFailure is null ? Output : throw OutputOpenFailure;
        public TextReader StandardError { get; } = new StringReader("synthetic-standard-error");
        public int ExitCode { get; init; }
        public bool WorkExited { get; init; } = true;
        public bool StartReturned { get; init; } = true;
        public bool? CleanupExited { get; init; }
        public Exception? StartFailure { get; init; }
        public Exception? KillFailure { get; init; }
        public Exception? CleanupWaitFailure { get; init; }
        public Exception? DisposeFailure { get; init; }
        public Exception? OutputOpenFailure { get; init; }
        public Action? KillAction { get; init; }
        public Action? DisposeAction { get; init; }
        public Action? BeforeWorkWait { get; init; }
        public bool Disposed { get; private set; }
        public ConcurrentQueue<int> WaitBudgets { get; } = new();

        public bool Start()
        {
            if (StartFailure is not null)
            {
                throw StartFailure;
            }

            return StartReturned;
        }

        public bool WaitForExit(int milliseconds)
        {
            WaitBudgets.Enqueue(milliseconds);
            if (WaitBudgets.Count == 1)
            {
                BeforeWorkWait?.Invoke();
                return WorkExited;
            }

            if (CleanupWaitFailure is not null)
            {
                throw CleanupWaitFailure;
            }

            return CleanupExited ?? KillFailure is null;
        }

        public void KillEntireProcessTree()
        {
            if (KillFailure is not null)
            {
                throw KillFailure;
            }

            KillAction?.Invoke();
        }

        public void Dispose()
        {
            if (DisposeFailure is not null)
            {
                throw DisposeFailure;
            }

            DisposeAction?.Invoke();
            Disposed = true;
            Output.Dispose();
            StandardError.Dispose();
        }
    }

    private sealed class FaultedReader : StringReader
    {
        private bool prefixReturned;

        public FaultedReader() : base(string.Empty) { }

        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!prefixReturned)
            {
                prefixReturned = true;
                const string prefix = "synthetic-partial-output";
                prefix.AsMemory().CopyTo(buffer);
                return ValueTask.FromResult(prefix.Length);
            }

            return ValueTask.FromException<int>(new IOException("synthetic-stdout-read-failure"));
        }
    }

    private sealed class PendingReader(bool ignoreCancellation, bool holdCancellationCompletion = false) : StringReader(string.Empty)
    {
        private readonly TaskCompletionSource<int> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool prefixReturned;
        internal ManualResetEventSlim ReadFinished { get; } = new();

        internal void EnsureReading() => Assert.True(reading.Task.Wait(2_000), "Synthetic reader did not reach its controlled pending read.");
        internal void Release() => release.TrySetResult(0);

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!prefixReturned)
            {
                prefixReturned = true;
                const string prefix = "synthetic-pending-prefix";
                prefix.AsMemory().CopyTo(buffer);
                return prefix.Length;
            }

            reading.TrySetResult();
            try
            {
                return ignoreCancellation
                    ? await release.Task.ConfigureAwait(false)
                    : await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (holdCancellationCompletion)
            {
                // A process-free control: completion stays held until the test's
                // finally releases this exact operation, not for a longer deadline.
                await release.Task.ConfigureAwait(false);
                throw;
            }
            finally
            {
                ReadFinished.Set();
            }
        }
    }
}
