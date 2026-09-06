// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Xunit.Abstractions;

namespace Foundry.Tests.Unit;

public sealed class FixtureProcessRunnerTests(ITestOutputHelper output)
{
    private static readonly FixtureProcessLimits ControlLimits = new(
        CleanupMilliseconds: 50,
        DrainMilliseconds: 50,
        SettlementMilliseconds: 250);

    [Fact]
    public async Task Async_wait_yields_before_the_owned_disposal_completes()
    {
        var scheduler = new ControlledDisposalScheduler();
        var process = new SyntheticProcess { StartReturned = false };
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule);
        Task<FixtureProcessResult>? run = null;
        FixtureProcessResult? result = null;
        FixtureProcessException? synchronousFailure = null;
        try
        {
            try { run = runner.RunAsync(() => process); }
            catch (FixtureProcessException failure)
            {
                synchronousFailure = failure;
                result = failure.Result;
                output.WriteLine(failure.ToString());
            }

            Assert.True(synchronousFailure is null && run is not null,
                "RunAsync consumed the disposal wait before returning control; " +
                "the caller could not release its queued owned disposal. " + synchronousFailure);
            Assert.True(scheduler.WasScheduled);
            Assert.False(run.IsCompleted);
            Assert.False(process.Disposed);
            scheduler.Execute();
            var expectedStartupFailure = await Assert.ThrowsAsync<FixtureProcessException>(() => run);
            result = expectedStartupFailure.Result;
            output.WriteLine(expectedStartupFailure.ToString());
            Assert.StartsWith("StartupFailure", result.PrimaryFailure, StringComparison.Ordinal);
            Assert.True(result.DisposalSettled);
            Assert.True(result.SafeToStartAnotherFixture);
            Assert.DoesNotContain(result.SecondaryOutcomes,
                item => item.StartsWith("DisposalDeadline:", StringComparison.Ordinal));
            Assert.True(process.Disposed);
        }
        finally
        {
            scheduler.Execute();
            if (run is not null)
            {
                try { result = await run; }
                catch (FixtureProcessException failure) { result = failure.Result; }
            }

            await AwaitSyntheticOperations(result, scheduler.Operation);
            output.WriteLine("Controlled disposal and the exact started-operation aggregate settled after release; original failure evidence is retained.");
        }
    }

    [Fact]
    public async Task Async_callers_cannot_overlap_owned_disposal()
    {
        var scheduler = new ControlledDisposalScheduler();
        var firstProcess = new SyntheticProcess { StartReturned = false };
        var secondProcess = new SyntheticProcess { StartReturned = false };
        var schedules = 0;
        var runner = new FixtureProcessRunner(disposalScheduler: callback =>
            ++schedules == 1 ? scheduler.Schedule(callback) : ExecuteDisposalImmediately(callback));
        var first = runner.RunAsync(() => firstProcess);
        Task<FixtureProcessResult>? second = null;
        FixtureProcessResult? firstResult = null;
        FixtureProcessResult? secondResult = null;
        var secondCreations = 0;
        Exception? primaryFailure = null;
        try
        {
            Assert.True(scheduler.WasScheduled);
            Assert.False(first.IsCompleted);
            second = runner.RunAsync(() =>
            {
                secondCreations++;
                Assert.True(firstProcess.Disposed, "A second factory overlapped the first owned disposal.");
                return secondProcess;
            });
            Assert.False(second.IsCompleted);
            Assert.Equal(0, secondCreations);
            scheduler.Execute();
            firstResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => first)).Result;
            secondResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => second)).Result;
            Assert.True(firstResult.SafeToStartAnotherFixture);
            Assert.True(secondResult.SafeToStartAnotherFixture);
            Assert.Equal(1, secondCreations);
            Assert.Equal(2, schedules);
        }
        catch (Exception failure)
        {
            primaryFailure = failure;
            throw;
        }
        finally
        {
            await ObserveNonoverlapCleanupAsync(first, second, scheduler, releaseAfterFirst: false,
                primaryFailure, () => secondCreations != 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Async_disposal_deadline_preserves_failure_and_refuses_sync_and_async_followers(bool synchronousFollower)
    {
        var scheduler = new ControlledDisposalScheduler();
        var process = new SyntheticProcess { StartReturned = false };
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule);
        var run = runner.RunWithFailureFollowUpAsync(() => process,
            _ => throw new IOException("synthetic-async-diagnostic-output-failure"));
        FixtureProcessResult? result = null;
        Task<FixtureProcessResult>? queuedFollower = null;
        var created = false;
        IFixtureProcess CreateUnexpected() { created = true; return new SyntheticProcess(); }
        try
        {
            if (!synchronousFollower)
            {
                queuedFollower = runner.RunAsync(CreateUnexpected);
                Assert.False(queuedFollower.IsCompleted);
                Assert.False(created);
            }

            var failure = await Assert.ThrowsAsync<FixtureProcessException>(() => run);
            result = failure.Result;
            output.WriteLine(failure.ToString());
            Assert.Equal(result.Describe(), failure.Message);
            Assert.StartsWith("StartupFailure", result.PrimaryFailure, StringComparison.Ordinal);
            Assert.Contains(result.SecondaryOutcomes,
                item => item.StartsWith("DisposalDeadline:", StringComparison.Ordinal));
            Assert.False(result.SafeToStartAnotherFixture);
            var frozen = result.Describe();
            var refusal = synchronousFollower
                ? Assert.Throws<InvalidOperationException>(() => runner.Run(CreateUnexpected))
                : await Assert.ThrowsAsync<InvalidOperationException>(() => queuedFollower!);
            Assert.False(created);
            Assert.EndsWith(frozen, refusal.Message, StringComparison.Ordinal);
        }
        finally
        {
            scheduler.Execute();
            result = await ObserveOwnedRunAsync(run);
            if (queuedFollower is not null)
            {
                try { await queuedFollower; }
                catch (InvalidOperationException failure) { output.WriteLine(failure.ToString()); }
            }

            await AwaitSyntheticOperations(result, scheduler.Operation);
        }

        Assert.True(process.Disposed);
        Assert.False(result.SafeToStartAnotherFixture);
        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public async Task A_synchronous_owner_and_async_follower_share_the_same_nonoverlap_gate()
    {
        var scheduler = new ControlledDisposalScheduler();
        var scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProcess = new SyntheticProcess { StartReturned = false };
        var secondProcess = new SyntheticProcess { StartReturned = false };
        var schedules = 0;
        var runner = new FixtureProcessRunner(disposalScheduler: callback =>
        {
            if (Interlocked.Increment(ref schedules) != 1) { return ExecuteDisposalImmediately(callback); }
            var task = scheduler.Schedule(callback);
            scheduled.SetResult();
            return task;
        });
        // One explicitly owned process-free worker invokes the compatibility
        // entry point; the controlling test must remain able to release disposal.
        var first = Task.Factory.StartNew(() => runner.Run(() => firstProcess),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<FixtureProcessResult>? second = null;
        FixtureProcessResult? firstResult = null;
        FixtureProcessResult? secondResult = null;
        var secondCreations = 0;
        Exception? primaryFailure = null;
        try
        {
            await scheduled.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(first.IsCompleted);
            second = runner.RunAsync(() =>
            {
                secondCreations++;
                Assert.True(firstProcess.Disposed, "The asynchronous factory overlapped the synchronous owner's disposal.");
                return secondProcess;
            });
            Assert.False(second.IsCompleted);
            Assert.Equal(0, secondCreations);
            scheduler.Execute();
            firstResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => first)).Result;
            secondResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => second)).Result;
            Assert.True(firstResult.SafeToStartAnotherFixture);
            Assert.True(secondResult.SafeToStartAnotherFixture);
            Assert.Equal(1, secondCreations);
        }
        catch (Exception failure)
        {
            primaryFailure = failure;
            throw;
        }
        finally
        {
            await ObserveNonoverlapCleanupAsync(first, second, scheduler, releaseAfterFirst: true,
                primaryFailure, () => secondCreations != 0);
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("timeout-fault")]
    [InlineData("io-fault")]
    [InlineData("canceled")]
    public async Task Async_wait_distinguishes_terminal_owned_outcomes_from_its_deadline(string outcome)
    {
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = FixtureProcessRunner.WaitSettledAsync(operation.Task, 250);
        try
        {
            Assert.False(wait.IsCompleted);
            switch (outcome)
            {
                case "success": operation.SetResult(); break;
                case "timeout-fault": operation.SetException(new TimeoutException("synthetic-owned-timeout-fault")); break;
                case "io-fault": operation.SetException(new IOException("synthetic-owned-io-fault")); break;
                case "canceled": operation.SetCanceled(); break;
            }

            Assert.True(await wait);
            Assert.Equal(outcome == "success", operation.Task.IsCompletedSuccessfully);
            Assert.Equal(outcome == "canceled", operation.Task.IsCanceled);
            if (outcome.EndsWith("-fault", StringComparison.Ordinal))
            {
                Assert.True(operation.Task.IsFaulted);
                Assert.Contains("synthetic-owned-", operation.Task.Exception.ToString(), StringComparison.Ordinal);
            }
        }
        finally
        {
            operation.TrySetResult();
            await wait;
            try { await operation.Task; }
            catch (Exception failure) when (failure is IOException or TimeoutException or TaskCanceledException)
            {
                output.WriteLine("Exact injected terminal operation retained: " + failure);
            }
        }
    }

    [Fact]
    public async Task Async_wait_keeps_an_expired_deadline_false_after_later_owned_completion()
    {
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = FixtureProcessRunner.WaitSettledAsync(operation.Task, 50);
        try
        {
            Assert.False(await wait);
            Assert.False(operation.Task.IsCompleted);
            operation.SetResult();
            await operation.Task;
            Assert.False(await wait);
        }
        finally
        {
            operation.TrySetResult();
            await operation.Task;
            await wait;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Async_wait_preserves_the_existing_completed_first_zero_budget_rule(bool completed)
    {
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (completed) { operation.SetResult(); }
            Assert.Equal(completed, await FixtureProcessRunner.WaitSettledAsync(operation.Task, 0));
        }
        finally
        {
            operation.TrySetResult();
            await operation.Task;
        }
    }

    [Fact]
    public async Task Async_disposal_timeout_exception_is_a_settled_fault_not_the_wait_deadline()
    {
        var process = new SyntheticProcess
        {
            StartReturned = false,
            DisposeFailure = new TimeoutException("synthetic-owned-disposal-timeout"),
        };
        var runner = new FixtureProcessRunner(ControlLimits, ExecuteDisposalImmediately);
        var failure = await Assert.ThrowsAsync<FixtureProcessException>(() => runner.RunAsync(() => process));
        var result = failure.Result;
        output.WriteLine(failure.ToString());
        Assert.StartsWith("StartupFailure", result.PrimaryFailure, StringComparison.Ordinal);
        Assert.True(result.DisposalSettled);
        Assert.False(result.SafeToStartAnotherFixture);
        Assert.Contains(result.SecondaryOutcomes,
            item => item.Contains("synthetic-owned-disposal-timeout", StringComparison.Ordinal));
        Assert.DoesNotContain(result.SecondaryOutcomes,
            item => item.StartsWith("DisposalDeadline:", StringComparison.Ordinal));
        var terminal = await Assert.ThrowsAsync<TimeoutException>(() => result.StartedOperations);
        Assert.Equal("synthetic-owned-disposal-timeout", terminal.Message);
        AssertRefusesNextCreation(runner);
    }

    private async Task<FixtureProcessResult> ObserveOwnedRunAsync(Task<FixtureProcessResult> run)
    {
        try { return await run; }
        catch (FixtureProcessException failure)
        {
            output.WriteLine(failure.ToString());
            return failure.Result;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Nonoverlap_cleanup_preserves_primary_failure_and_observes_owned_aggregates(
        bool releaseAfterFirst,
        bool fixturePrimary)
    {
        var scheduler = new ControlledDisposalScheduler();
        var process = new SyntheticProcess { StartReturned = false };
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule,
            () => new ManualSettlementClock());
        var first = runner.RunAsync(() => process);
        var followerCreated = false;
        var second = runner.RunAsync(() =>
        {
            followerCreated = true;
            return new SyntheticProcess();
        });
        FixtureProcessResult? firstResult = null;
        try
        {
            var firstFailure = await Assert.ThrowsAsync<FixtureProcessException>(() => first);
            firstResult = firstFailure.Result;
            Assert.False(firstResult.SafeToStartAnotherFixture);
            Assert.False(process.Disposed);
            Exception primary = fixturePrimary
                ? firstFailure
                : new Xunit.Sdk.XunitException("synthetic-original-nonoverlap-assertion");
            var observedAggregates = new List<string>();
            IReadOnlyList<Exception>? cleanupFailures = null;
            var escaping = await Record.ExceptionAsync(async () =>
            {
                try { throw primary; }
                finally
                {
                    cleanupFailures = await ObserveNonoverlapCleanupAsync(first, second, scheduler,
                        releaseAfterFirst, primary, () => followerCreated, observedAggregates.Add);
                }
            });
            output.WriteLine("Original injected primary: " + primary);
            output.WriteLine("Escaping measured cleanup scope: " + escaping);
            output.WriteLine("Aggregate observation sites reached: " + string.Join(",", observedAggregates));
            Assert.Same(primary, escaping);
            Assert.Equal(["first-owned-aggregate-observed", "second-owned-aggregate-observed"], observedAggregates);
            Assert.False(followerCreated);
            Assert.Empty(cleanupFailures!);
        }
        finally
        {
            scheduler.Execute();
            try { firstResult = await first; }
            catch (FixtureProcessException failure) { firstResult = failure.Result; }
            scheduler.Execute();
            try { await second; }
            catch (InvalidOperationException refusal) when (firstResult is { SafeToStartAnotherFixture: false }
                && !followerCreated
                && string.Equals(refusal.Message,
                    "A prior fixture has uncertain cleanup/capture; no new process was created. " +
                    "A fresh test host is required after ownership is resolved." + Environment.NewLine + firstResult.Describe(),
                    StringComparison.Ordinal))
            {
                output.WriteLine("Outer guaranteed-cleanup observed the exact sticky no-process refusal: " + refusal);
            }

            await AwaitSyntheticOperations(firstResult, scheduler.Operation);
            output.WriteLine("Outer guaranteed-cleanup awaited the exact owned aggregate after release; a skipped inner observation site does not itself prove an unsettled task.");
        }
    }

    [Theory]
    [InlineData("unrelated", false)]
    [InlineData("unrelated", true)]
    [InlineData("altered-result", false)]
    [InlineData("altered-result", true)]
    [InlineData("factory-entered", false)]
    [InlineData("factory-entered", true)]
    public async Task Nonoverlap_cleanup_keeps_unexpected_refusals_failing_and_observes_every_aggregate(
        string refusalKind,
        bool hasPrimary)
    {
        var scheduler = new ControlledDisposalScheduler();
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule,
            () => new ManualSettlementClock());
        var first = runner.RunAsync(() => new SyntheticProcess { StartReturned = false });
        FixtureProcessResult? firstResult = null;
        Task<FixtureProcessResult>? second = null;
        InvalidOperationException? injected = null;
        try
        {
            firstResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => first)).Result;
            Assert.False(firstResult.SafeToStartAnotherFixture);
            // This negative control injects the follower outcome only. The
            // positive sticky-refusal cases above use the actual shared runner.
            injected = new InvalidOperationException(refusalKind switch
            {
                "unrelated" => "synthetic-unrelated-invalid-operation",
                "altered-result" => StickyRefusalMessage(firstResult) + "synthetic-altered-frozen-result",
                "factory-entered" => StickyRefusalMessage(firstResult),
                _ => throw new ArgumentOutOfRangeException(nameof(refusalKind)),
            });
            second = Task.FromException<FixtureProcessResult>(injected);
            Exception? primary = hasPrimary ? new Xunit.Sdk.XunitException("synthetic-prior-control-assertion") : null;
            var aggregateObservations = new List<string>();
            var diagnostics = new List<string>();
            IReadOnlyList<Exception>? cleanupFailures = null;
            var escaping = await Record.ExceptionAsync(async () =>
            {
                try { if (primary is not null) { throw primary; } }
                finally
                {
                    cleanupFailures = await ObserveNonoverlapCleanupAsync(first, second, scheduler,
                        releaseAfterFirst: false, primary, () => refusalKind == "factory-entered",
                        aggregateObservations.Add, diagnostics.Add);
                }
            });

            Assert.Same(primary ?? injected, escaping);
            Assert.Equal(["first-owned-aggregate-observed", "second-owned-aggregate-observed"], aggregateObservations);
            Assert.Contains(diagnostics, message => message.StartsWith("Secondary non-overlap cleanup failure (follower outcome):", StringComparison.Ordinal)
                && message.Contains(injected.Message, StringComparison.Ordinal));
            Assert.DoesNotContain(diagnostics, message => message.StartsWith("Observed exact sticky refusal", StringComparison.Ordinal));
            if (hasPrimary) { Assert.Same(injected, Assert.Single(cleanupFailures!)); }
            output.WriteLine("Injected unexpected follower outcome retained separately: " + injected);
            output.WriteLine("Escaping verdict after both aggregate observations: " + escaping);
        }
        finally
        {
            scheduler.Execute();
            try { firstResult = await first; }
            catch (FixtureProcessException failure) { firstResult = failure.Result; }
            scheduler.Execute();
            if (second is not null)
            {
                try { await second; }
                catch (InvalidOperationException failure) when (ReferenceEquals(failure, injected)) { }
            }

            await AwaitSyntheticOperations(firstResult, scheduler.Operation);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nonoverlap_cleanup_observes_both_real_owned_aggregates_when_diagnostics_throw(bool hasPrimary)
    {
        var firstScheduler = new ControlledDisposalScheduler();
        var secondScheduler = new ControlledDisposalScheduler();
        // Independent synthetic owners provide two actual, initially unsettled
        // aggregates; this is not a claim that one unsafe runner admits a second.
        var firstRunner = new FixtureProcessRunner(ControlLimits, firstScheduler.Schedule,
            () => new ManualSettlementClock());
        var secondRunner = new FixtureProcessRunner(ControlLimits, secondScheduler.Schedule,
            () => new ManualSettlementClock());
        var first = firstRunner.RunAsync(() => new SyntheticProcess { StartReturned = false });
        var second = secondRunner.RunAsync(() => new SyntheticProcess { StartReturned = false });
        FixtureProcessResult? firstResult = null;
        FixtureProcessResult? secondResult = null;
        try
        {
            firstResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => first)).Result;
            secondResult = (await Assert.ThrowsAsync<FixtureProcessException>(() => second)).Result;
            Assert.False(firstResult.StartedOperations.IsCompleted);
            Assert.False(secondResult.StartedOperations.IsCompleted);
            var injected = new IOException("synthetic-nonoverlap-diagnostic-writer-failure");
            void failingDiagnostic(string _) { throw injected; }
            Exception? primary = hasPrimary ? new Xunit.Sdk.XunitException("synthetic-primary-before-writer-failure") : null;
            var aggregateObservations = new List<string>();
            IReadOnlyList<Exception>? cleanupFailures = null;
            var escaping = await Record.ExceptionAsync(async () =>
            {
                try { if (primary is not null) { throw primary; } }
                finally
                {
                    cleanupFailures = await ObserveNonoverlapCleanupAsync(first, second, firstScheduler,
                        releaseAfterFirst: false, primary, () => true,
                        stage =>
                        {
                            aggregateObservations.Add(stage);
                            if (stage == "first-owned-aggregate-observed") { secondScheduler.Execute(); }
                        },
failingDiagnostic);
                }
            });

            Assert.Same(primary ?? injected, escaping);
            Assert.Equal(["first-owned-aggregate-observed", "second-owned-aggregate-observed"], aggregateObservations);
            Assert.True(firstResult.StartedOperations.IsCompleted);
            Assert.True(secondResult.StartedOperations.IsCompleted);
            if (hasPrimary)
            {
                Assert.Equal(3, cleanupFailures!.Count);
                Assert.All(cleanupFailures, failure => Assert.Same(injected, failure));
            }

            output.WriteLine("Injected diagnostic failure retained without aborting either exact owned-aggregate observation: " + injected);
            output.WriteLine("Escaping verdict: " + escaping);
        }
        finally
        {
            firstScheduler.Execute();
            secondScheduler.Execute();
            try { firstResult = await first; }
            catch (FixtureProcessException failure) { firstResult = failure.Result; }
            try { secondResult = await second; }
            catch (FixtureProcessException failure) { secondResult = failure.Result; }
            firstScheduler.Execute();
            secondScheduler.Execute();
            // Independent finally clauses prevent one observation failure from
            // skipping the other owner's guaranteed synthetic cleanup.
            try { await AwaitSyntheticOperations(firstResult, firstScheduler.Operation); }
            finally { await AwaitSyntheticOperations(secondResult, secondScheduler.Operation); }
        }
    }

    private async Task<IReadOnlyList<Exception>> ObserveNonoverlapCleanupAsync(
        Task<FixtureProcessResult> first,
        Task<FixtureProcessResult>? second,
        ControlledDisposalScheduler scheduler,
        bool releaseAfterFirst,
        Exception? primaryFailure,
        Func<bool> followerFactoryEntered,
        Action<string>? observeAggregate = null,
        Action<string>? writeDiagnostic = null)
    {
        // Only these two non-overlap controls use this cleanup policy. It does
        // not alter runner outcomes or make an unsafe fixture reusable. Every
        // owned aggregate is attempted even after an observation/write failure.
        var failures = new List<Exception>();
        var write = writeDiagnostic ?? output.WriteLine;
        FixtureProcessResult? firstResult = null;
        FixtureProcessResult? secondResult = null;
        if (primaryFailure is not null) { Report("Original non-overlap control failure: " + primaryFailure); }
        await AttemptAsync("initial release", () => { scheduler.Execute(); return Task.CompletedTask; });
        firstResult = await ObserveRunAsync(first, isFollower: false);
        // A delayed synchronous owner can reach Schedule after the first release.
        if (releaseAfterFirst)
        {
            await AttemptAsync("delayed-owner release", () => { scheduler.Execute(); return Task.CompletedTask; });
        }

        if (second is not null) { secondResult = await ObserveRunAsync(second, isFollower: true); }
        await AttemptAsync("first owned aggregate", async () =>
        {
            await AwaitSyntheticOperations(firstResult, scheduler.Operation);
            observeAggregate?.Invoke("first-owned-aggregate-observed");
        });
        await AttemptAsync("second owned aggregate", async () =>
        {
            await AwaitSyntheticOperations(secondResult);
            observeAggregate?.Invoke("second-owned-aggregate-observed");
        });

        // An already propagating assertion/fixture failure remains the primary.
        // Unexpected cleanup failures are reported and returned separately. With
        // no primary, the first unexpected failure is rethrown after all attempts.
        if (primaryFailure is null && failures.Count > 0)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        return Array.AsReadOnly(failures.ToArray());

        void Report(string message)
        {
            try { write(message); }
            catch (Exception failure) { failures.Add(failure); }
        }

        void RetainUnexpected(string stage, Exception failure)
        {
            failures.Add(failure);
            Report($"Secondary non-overlap cleanup failure ({stage}): {failure}");
        }

        async Task AttemptAsync(string stage, Func<Task> attempt)
        {
            try { await attempt(); }
            catch (Exception failure) { RetainUnexpected(stage, failure); }
        }

        async Task<FixtureProcessResult?> ObserveRunAsync(Task<FixtureProcessResult> run, bool isFollower)
        {
            try { return await run; }
            catch (FixtureProcessException failure)
            {
                // Capture the result before attempting a fallible diagnostic write.
                var result = failure.Result;
                Report("Observed owned non-overlap fixture outcome: " + failure);
                return result;
            }
            catch (InvalidOperationException refusal) when (isFollower
                && refusal.GetType() == typeof(InvalidOperationException)
                && firstResult is { SafeToStartAnotherFixture: false }
                && !followerFactoryEntered()
                && string.Equals(refusal.Message, StickyRefusalMessage(firstResult), StringComparison.Ordinal))
            {
                Report("Observed exact sticky refusal; follower factory was not entered: " + refusal);
                return null;
            }
            catch (Exception failure)
            {
                RetainUnexpected(isFollower ? "follower outcome" : "first outcome", failure);
                return null;
            }
        }
    }

    private static string StickyRefusalMessage(FixtureProcessResult result)
        => "A prior fixture has uncertain cleanup/capture; no new process was created. " +
            "A fresh test host is required after ownership is resolved." + Environment.NewLine + result.Describe();

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_observation_distinguishes_queued_callback_and_freezes_before_late_entry(bool faultAfterEntry)
    {
        var clock = new ManualSettlementClock();
        var scheduler = new ControlledDisposalScheduler();
        FixtureProcessRunner? runner = null;
        FixtureDisposalFollowUpObservation? duringDisposal = null;
        var process = new SyntheticProcess
        {
            DisposeAction = () =>
            {
                duringDisposal = runner!.ObserveRetainedDisposal();
                if (faultAfterEntry)
                {
                    throw new IOException("synthetic-late-disposal-fault");
                }
            },
        };
        runner = new FixtureProcessRunner(
            new FixtureProcessLimits(SettlementMilliseconds: 250), scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        string? frozenDescription = null;
        FixtureDisposalFollowUpObservation? frozenFollowUp = null;
        string? frozenFollowUpDescription = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => process);
            frozenDescription = result.Describe();
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.False(process.Disposed);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.Queued, observation.Stage);
            Assert.Equal(250, observation.RemainingAtDecisionMilliseconds);
            Assert.Equal(250, observation.RemainingAtWaitMilliseconds);
            Assert.Null(observation.CallbackEntryElapsedMilliseconds);
            Assert.Null(observation.CallbackExitElapsedMilliseconds);
            Assert.False(observation.TaskCompletionObserved);
            Assert.False(observation.WaitReturnedSettled);
            frozenFollowUp = ObserveFollowUp(runner);
            frozenFollowUpDescription = frozenFollowUp.Describe();
            Assert.Equal(FixtureDisposalStage.Queued, frozenFollowUp.Stage);
            Assert.False(frozenFollowUp.TaskCompletionObserved);
            Assert.Null(frozenFollowUp.CallbackEntryElapsedMilliseconds);
            Assert.Equal(observation.SnapshotElapsedMilliseconds, frozenFollowUp.OriginalSnapshotElapsedMilliseconds);
            clock.AdvanceTo(10);
            var stillQueued = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.Queued, stillQueued.Stage);
            Assert.False(stillQueued.TaskCompletionObserved);
            Assert.Null(stillQueued.CallbackEntryElapsedMilliseconds);
            Assert.Equal(frozenFollowUp.ObservationNumber + 1, stillQueued.ObservationNumber);
            Assert.NotSame(frozenFollowUp, stillQueued);
        }
        finally
        {
            clock.AdvanceTo(300);
            scheduler.Execute();
            await AwaitSyntheticOperations(result, scheduler.Operation);
        }

        var completed = ObserveFollowUp(runner);
        Assert.Equal(FixtureDisposalStage.CallbackExited, completed.Stage);
        Assert.Equal(faultAfterEntry ? TaskStatus.Faulted : TaskStatus.RanToCompletion, completed.DisposalTaskStatus);
        Assert.True(completed.TaskCompletionObserved);
        Assert.Equal(faultAfterEntry, completed.TaskFaultObserved);
        if (faultAfterEntry)
        {
            Assert.Contains("synthetic-late-disposal-fault", completed.TaskFailure, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(completed.TaskFailure);
        }
        Assert.Equal(300, completed.CallbackEntryElapsedMilliseconds);
        Assert.Equal(300, completed.CallbackExitElapsedMilliseconds);
        var enteredAfterOriginal = Assert.IsType<FixtureDisposalFollowUpObservation>(duringDisposal);
        output.WriteLine(enteredAfterOriginal.Describe());
        Assert.Equal(FixtureDisposalStage.Entered, enteredAfterOriginal.Stage);
        Assert.False(enteredAfterOriginal.TaskCompletionObserved);
        Assert.Equal(300, enteredAfterOriginal.CallbackEntryElapsedMilliseconds);
        Assert.Null(enteredAfterOriginal.CallbackExitElapsedMilliseconds);
        Assert.Equal(frozenFollowUpDescription, frozenFollowUp!.Describe());
        Assert.Equal(!faultAfterEntry, process.Disposed);
        Assert.Equal(frozenDescription, result.Describe());
        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public async Task Disposal_observation_distinguishes_entered_callback_from_unsettled_task()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var clock = new ManualSettlementClock();
        var process = new SyntheticProcess
        {
            DisposeAction = () => { entered.Set(); release.Wait(); },
        };
        Task operation = Task.CompletedTask;
        Task Schedule(Action callback)
        {
            operation = Task.Run(callback);
            Assert.True(entered.Wait(2_000), "Synthetic disposal did not reach its explicit entry gate.");
            return operation;
        }
        var runner = new FixtureProcessRunner(
            new FixtureProcessLimits(SettlementMilliseconds: 250), Schedule, () => clock);
        FixtureProcessResult? result = null;
        FixtureDisposalFollowUpObservation? enteredFollowUp = null;
        string? frozenDescription = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => process);
            Assert.False(result.DisposalSettled);
            Assert.False(result.SafeToStartAnotherFixture);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.Entered, observation.Stage);
            Assert.Equal(0, observation.CallbackEntryElapsedMilliseconds);
            Assert.Null(observation.CallbackExitElapsedMilliseconds);
            Assert.False(observation.TaskCompletionObserved);
            Assert.False(observation.WaitReturnedSettled);
            frozenDescription = result.Describe();
            enteredFollowUp = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.Entered, enteredFollowUp.Stage);
            Assert.False(enteredFollowUp.TaskCompletionObserved);
            Assert.Equal(0, enteredFollowUp.CallbackEntryElapsedMilliseconds);
            Assert.Null(enteredFollowUp.CallbackExitElapsedMilliseconds);
        }
        finally
        {
            clock.AdvanceTo(300);
            release.Set();
            await AwaitSyntheticOperations(result, operation);
        }

        var exitedFollowUp = ObserveFollowUp(runner);
        Assert.Equal(FixtureDisposalStage.CallbackExited, exitedFollowUp.Stage);
        Assert.True(exitedFollowUp.TaskCompletionObserved);
        Assert.Equal(300, exitedFollowUp.CallbackExitElapsedMilliseconds);
        Assert.Null(enteredFollowUp.CallbackExitElapsedMilliseconds);
        Assert.False(enteredFollowUp.TaskCompletionObserved);
        Assert.Equal(frozenDescription, result.Describe());
        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public async Task Disposal_observation_identifies_budget_exhausted_before_scheduling()
    {
        var clock = new ManualSettlementClock(250);
        var scheduler = new ControlledDisposalScheduler();
        var process = new SyntheticProcess();
        var runner = new FixtureProcessRunner(
            new FixtureProcessLimits(SettlementMilliseconds: 250), scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => process);
            Assert.True(result.CaptureSettled);
            Assert.False(scheduler.WasScheduled);
            Assert.False(result.SafeToStartAnotherFixture);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.Deferred, observation.Stage);
            Assert.Equal(FixtureDisposalDeferral.SettlementBudgetExhausted, observation.DeferredReasons);
            Assert.Equal(250, observation.DecisionElapsedMilliseconds);
            Assert.Equal(0, observation.RemainingAtDecisionMilliseconds);
            Assert.Null(observation.WaitElapsedMilliseconds);
            Assert.Null(observation.RemainingAtWaitMilliseconds);
            Assert.Null(observation.CallbackEntryElapsedMilliseconds);
            var followUp = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.Deferred, followUp.Stage);
            Assert.Null(followUp.DisposalTaskStatus);
            Assert.False(followUp.TaskCompletionObserved);
            Assert.Null(followUp.CallbackEntryElapsedMilliseconds);
            Assert.False(scheduler.WasScheduled);
        }
        finally
        {
            scheduler.Execute();
            await AwaitSyntheticOperations(result, scheduler.Operation);
            process.Dispose();
        }
    }

    [Fact]
    public async Task Disposal_observation_identifies_budget_consumed_between_scheduling_and_wait()
    {
        var clock = new ManualSettlementClock();
        var scheduler = new ControlledDisposalScheduler { AfterScheduling = () => clock.AdvanceTo(250) };
        var runner = new FixtureProcessRunner(
            new FixtureProcessLimits(SettlementMilliseconds: 250), scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => new SyntheticProcess());
            Assert.False(result.SafeToStartAnotherFixture);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.Queued, observation.Stage);
            Assert.Equal(0, observation.DecisionElapsedMilliseconds);
            Assert.Equal(250, observation.RemainingAtDecisionMilliseconds);
            Assert.Equal(250, observation.WaitElapsedMilliseconds);
            Assert.Equal(0, observation.RemainingAtWaitMilliseconds);
            Assert.Null(observation.CallbackEntryElapsedMilliseconds);
            Assert.False(observation.WaitReturnedSettled);
        }
        finally
        {
            scheduler.Execute();
            await AwaitSyntheticOperations(result, scheduler.Operation);
        }
    }

    [Fact]
    public async Task Disposal_observation_identifies_unsettled_capture_without_inventing_budget_exhaustion()
    {
        var reader = new PendingReader(ignoreCancellation: true);
        var process = new SyntheticProcess { Output = reader, BeforeWorkWait = reader.EnsureReading };
        var clock = new ManualSettlementClock();
        var scheduler = new ControlledDisposalScheduler();
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => process);
            Assert.False(result.CaptureSettled);
            Assert.False(scheduler.WasScheduled);
            Assert.False(result.SafeToStartAnotherFixture);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.Deferred, observation.Stage);
            Assert.Equal(FixtureDisposalDeferral.CaptureUnsettled, observation.DeferredReasons);
            Assert.Equal(250, observation.RemainingAtDecisionMilliseconds);
            Assert.Null(observation.WaitElapsedMilliseconds);
        }
        finally
        {
            reader.Release();
            scheduler.Execute();
            await AwaitSyntheticOperations(result, scheduler.Operation);
            process.Dispose();
        }
    }

    [Fact]
    public async Task Disposal_observation_keeps_zero_elapsed_entry_exit_and_task_completion_distinct()
    {
        // A known-unstarted adapter isolates disposal observations from real
        // capture scheduling. This is not started-process or stream-drain proof.
        var disposalCalls = 0;
        var process = new SyntheticProcess
        {
            StartReturned = false,
            DisposeAction = () => Interlocked.Increment(ref disposalCalls),
        };
        var clock = new ManualSettlementClock();
        var runner = new FixtureProcessRunner(ControlLimits, ExecuteDisposalImmediately, () => clock);
        FixtureProcessResult? result = null;
        Task? startedOperations = null;
        try
        {
            output.WriteLine("Known-unstarted synthetic adapter; no started-process or capture-scheduling proof:");
            result = ObserveSyntheticRun(runner, () => process);
            startedOperations = result.StartedOperations;
            Assert.Equal("StartupFailure: the process API returned false.", result.PrimaryFailure);
            var unavailable = new FixtureStreamSnapshot(string.Empty, "Unavailable", null, false);
            Assert.Equal(unavailable, result.Output);
            Assert.Equal(unavailable, result.Error);
            Assert.False(result.RootExitObserved);
            Assert.Null(result.ExitCode);
            Assert.Empty(process.WaitBudgets);
            Assert.Empty(result.SecondaryOutcomes);
            Assert.True(process.Disposed);
            Assert.Equal(1, Volatile.Read(ref disposalCalls));
            Assert.True(result.DisposalSettled);
            Assert.True(result.SafeToStartAnotherFixture);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.CallbackExited, observation.Stage);
            Assert.Equal(FixtureDisposalDeferral.None, observation.DeferredReasons);
            Assert.Equal(0, observation.DecisionElapsedMilliseconds);
            Assert.Equal(0, observation.WaitElapsedMilliseconds);
            Assert.Equal(0, observation.CallbackEntryElapsedMilliseconds);
            Assert.Equal(0, observation.CallbackExitElapsedMilliseconds);
            Assert.Equal(0, observation.SnapshotElapsedMilliseconds);
            Assert.True(observation.TaskCompletionObserved);
            Assert.False(observation.TaskFaultObserved);
            Assert.True(observation.WaitReturnedSettled);
        }
        finally
        {
            if (startedOperations is not null)
            {
                await startedOperations.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.True(startedOperations.IsCompletedSuccessfully);
                output.WriteLine("Known-unstarted adapter's exact StartedOperations aggregate settled; the captured verdict is unchanged.");
                if (!process.Disposed)
                {
                    // Only after the exact owned operations settle may this
                    // control clean up its adapter; no failed verdict is cleared.
                    process.Dispose();
                }
            }
        }
    }

    [Fact]
    public async Task Disposal_observation_does_not_promote_callback_exit_to_task_completion()
    {
        var clock = new ManualSettlementClock();
        var scheduler = new ControlledDisposalScheduler { InvokeCallbackBeforeReturning = true };
        var runner = new FixtureProcessRunner(
            new FixtureProcessLimits(SettlementMilliseconds: 250), scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        FixtureDisposalFollowUpObservation? callbackOnlyFollowUp = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => new SyntheticProcess());
            Assert.False(result.DisposalSettled);
            Assert.False(result.SafeToStartAnotherFixture);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.CallbackExited, observation.Stage);
            Assert.Equal(0, observation.CallbackEntryElapsedMilliseconds);
            Assert.Equal(0, observation.CallbackExitElapsedMilliseconds);
            Assert.False(observation.TaskCompletionObserved);
            Assert.False(observation.WaitReturnedSettled);
            callbackOnlyFollowUp = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.CallbackExited, callbackOnlyFollowUp.Stage);
            Assert.False(callbackOnlyFollowUp.TaskCompletionObserved);
        }
        finally
        {
            scheduler.Execute();
            await AwaitSyntheticOperations(result, scheduler.Operation);
        }

        Assert.False(result!.DisposalObservation!.TaskCompletionObserved);
        var taskCompletedFollowUp = ObserveFollowUp(runner);
        Assert.Equal(FixtureDisposalStage.CallbackExited, taskCompletedFollowUp.Stage);
        Assert.True(taskCompletedFollowUp.TaskCompletionObserved);
        Assert.False(callbackOnlyFollowUp!.TaskCompletionObserved);
        AssertRefusesNextCreation(runner);
    }

    [Fact]
    public async Task Disposal_observation_preserves_faulted_callback_exit_without_calling_it_successful()
    {
        var clock = new ManualSettlementClock();
        var runner = new FixtureProcessRunner(ControlLimits, ExecuteDisposalImmediately, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => new SyntheticProcess
            {
                DisposeFailure = new IOException("synthetic-observed-disposal-fault"),
            });
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.Contains("synthetic-observed-disposal-fault", result.Describe(), StringComparison.Ordinal);
            AssertRefusesNextCreation(runner);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.CallbackExited, observation.Stage);
            Assert.Equal(0, observation.CallbackEntryElapsedMilliseconds);
            Assert.Equal(0, observation.CallbackExitElapsedMilliseconds);
            Assert.True(observation.TaskCompletionObserved);
            Assert.True(observation.TaskFaultObserved);
            var followUp = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.CallbackExited, followUp.Stage);
            Assert.Equal(TaskStatus.Faulted, followUp.DisposalTaskStatus);
            Assert.True(followUp.TaskCompletionObserved);
            Assert.True(followUp.TaskFaultObserved);
            Assert.Contains("synthetic-observed-disposal-fault", followUp.TaskFailure, StringComparison.Ordinal);
        }
        finally
        {
            await AwaitSyntheticOperations(result);
        }
    }

    [Fact]
    public async Task Disposal_observation_does_not_claim_timeliness_from_completion_with_zero_wait_budget()
    {
        var clock = new ManualSettlementClock();
        var runner = new FixtureProcessRunner(ControlLimits, ExecuteDisposalImmediately, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => new SyntheticProcess
            {
                DisposeAction = () => clock.AdvanceTo(250),
            });
            var observation = RequireDisposalObservation(result);
            Assert.Equal(0, observation.CallbackEntryElapsedMilliseconds);
            Assert.Equal(250, observation.CallbackExitElapsedMilliseconds);
            Assert.Equal(0, observation.RemainingAtWaitMilliseconds);
            Assert.True(observation.TaskCompletionObserved);
            Assert.True(observation.WaitReturnedSettled);
            Assert.Contains("TimelySettlement: NotEstablishedByObservations", result.Describe(), StringComparison.Ordinal);
        }
        finally
        {
            await AwaitSyntheticOperations(result);
        }
    }

    [Fact]
    public async Task Disposal_observation_distinguishes_no_adapter_from_deferred_disposal()
    {
        var clock = new ManualSettlementClock();
        var scheduler = new ControlledDisposalScheduler();
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        try
        {
            result = ObserveSyntheticRun(runner, () => throw new IOException("synthetic-factory-fault"));
            Assert.False(scheduler.WasScheduled);
            var observation = RequireDisposalObservation(result);
            Assert.Equal(FixtureDisposalStage.NotRequired, observation.Stage);
            Assert.Equal(FixtureDisposalDeferral.None, observation.DeferredReasons);
            Assert.Equal(0, observation.DecisionElapsedMilliseconds);
            Assert.Equal(0, observation.SnapshotElapsedMilliseconds);
            Assert.Null(observation.CallbackEntryElapsedMilliseconds);
            Assert.Null(observation.CallbackExitElapsedMilliseconds);
            Assert.False(observation.TaskCompletionObserved);
            Assert.Null(observation.WaitReturnedSettled);
            var followUp = ObserveFollowUp(runner);
            Assert.Equal(FixtureDisposalStage.NotRequired, followUp.Stage);
            Assert.Null(followUp.DisposalTaskStatus);
            Assert.Null(followUp.CallbackEntryElapsedMilliseconds);
            Assert.Null(followUp.CallbackExitElapsedMilliseconds);
            Assert.False(followUp.TaskCompletionObserved);
            Assert.False(scheduler.WasScheduled);
        }
        finally
        {
            await AwaitSyntheticOperations(result);
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
    public async Task Native_childless_timeout_retains_owned_pid_exit_and_separate_streams_at_thirty_seconds()
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
        var exception = await Assert.ThrowsAsync<FixtureProcessException>(() => new FixtureProcessRunner().RunAsync(() => process));
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
        var logCalls = 0;
        var result = RunFailure(new SyntheticProcess
        {
            WorkExited = false,
            DisposeFailure = new IOException("synthetic-dispose-failure"),
        }, followUpWriter: _ =>
        {
            logCalls++;
            throw new IOException("synthetic-follow-up-output-failure");
        }).Result;

        Assert.Equal(1, logCalls);
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

    [Fact]
    public void A_follow_up_without_retained_ownership_is_absent()
    {
        Assert.Null(new FixtureProcessRunner().ObserveRetainedDisposal());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Follow_up_diagnostic_failures_do_not_replace_the_original_refusal(
        bool observationFails,
        bool loggingFails)
    {
        var clock = new ManualSettlementClock(250);
        var scheduler = new ControlledDisposalScheduler();
        var process = new SyntheticProcess { StartReturned = false };
        var runner = new FixtureProcessRunner(ControlLimits, scheduler.Schedule, () => clock);
        FixtureProcessResult? result = null;
        var factoryCalls = 0;
        try
        {
            result = ObserveSyntheticRun(runner, () => { factoryCalls++; return process; });
            var frozenDescription = result.Describe();
            var originalRefusal = Assert.Throws<InvalidOperationException>(() => runner.Run(
                () => { factoryCalls++; return process; }));
            clock.ReadFailure = observationFails ? new IOException("synthetic-follow-up-clock-failure") : null;
            var messages = new List<string>();
            var refusal = Assert.Throws<InvalidOperationException>(() => runner.RunWithFailureFollowUp(
                () => { factoryCalls++; return process; },
                message =>
                {
                    messages.Add(message);
                    if (loggingFails)
                    {
                        throw new IOException("synthetic-follow-up-output-failure");
                    }
                }));

            Assert.Equal(originalRefusal.Message, refusal.Message);
            Assert.Equal(frozenDescription, result.Describe());
            Assert.False(result.SafeToStartAnotherFixture);
            Assert.Equal(1, factoryCalls);
            Assert.False(scheduler.WasScheduled);
            var message = Assert.Single(messages);
            output.WriteLine(message);
            if (observationFails)
            {
                Assert.Contains("Follow-up observation failed", message, StringComparison.Ordinal);
                Assert.Contains("synthetic-follow-up-clock-failure", message, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("Later disposal follow-up", message, StringComparison.Ordinal);
                Assert.Contains("Stage: Deferred", message, StringComparison.Ordinal);
            }
        }
        finally
        {
            clock.ReadFailure = null;
            await AwaitSyntheticOperations(result, scheduler.Operation);
            process.Dispose();
        }

        AssertRefusesNextCreation(runner);
    }

    private FixtureDisposalFollowUpObservation ObserveFollowUp(FixtureProcessRunner runner)
    {
        var observation = Assert.IsType<FixtureDisposalFollowUpObservation>(runner.ObserveRetainedDisposal());
        output.WriteLine(observation.Describe());
        Assert.Contains("diagnostic-only; sequential observations", observation.Describe(), StringComparison.Ordinal);
        Assert.Contains("TaskCompletionTime: NotMeasured", observation.Describe(), StringComparison.Ordinal);
        Assert.Contains("TimelySettlement: NotEstablishedByFollowUp", observation.Describe(), StringComparison.Ordinal);
        Assert.True(observation.AfterProgressObservationElapsedMilliseconds >= observation.BeforeTaskObservationElapsedMilliseconds);
        return observation;
    }

    private FixtureProcessResult ObserveSyntheticRun(FixtureProcessRunner runner, Func<IFixtureProcess> createProcess)
    {
        FixtureProcessResult result;
        try { result = runner.Run(createProcess); }
        catch (FixtureProcessException failure) { result = failure.Result; }
        output.WriteLine("Synthetic disposal/clock control only; not a hosted scheduling-cause finding:");
        output.WriteLine(result.Describe());
        return result;
    }

    private static FixtureDisposalObservation RequireDisposalObservation(FixtureProcessResult result)
    {
        Assert.True(result.DisposalObservation is not null,
            "The fixture result does not retain disposal scheduling, callback progress, and shared-budget observations.");
        return result.DisposalObservation;
    }

    private static Task ExecuteDisposalImmediately(Action callback)
    {
        try { callback(); return Task.CompletedTask; }
        catch (Exception failure) { return Task.FromException(failure); }
    }

    private static async Task AwaitSyntheticOperations(FixtureProcessResult? result, Task? scheduled = null)
    {
        var operations = Task.WhenAll(result?.StartedOperations ?? Task.CompletedTask, scheduled ?? Task.CompletedTask);
        try { await operations.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (IOException) when (operations.IsFaulted)
        {
            // The injected disposal fault is retained in the result, not relabelled
            // success. A wait timeout or any unexpected failure must escape.
        }
    }

    private sealed class ManualSettlementClock(long initialMilliseconds = 0) : IFixtureSettlementClock
    {
        private long elapsed = initialMilliseconds;
        internal Exception? ReadFailure { get; set; }
        public long ElapsedMilliseconds => ReadFailure is null ? Interlocked.Read(ref elapsed) : throw ReadFailure;
        internal void AdvanceTo(long milliseconds)
        {
            Assert.True(milliseconds >= ElapsedMilliseconds, "Synthetic monotonic clock cannot move backwards.");
            Interlocked.Exchange(ref elapsed, milliseconds);
        }
    }

    private sealed class ControlledDisposalScheduler
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Action? callback;
        private bool callbackInvoked;
        private Exception? callbackFailure;
        internal Action? AfterScheduling { get; init; }
        internal bool InvokeCallbackBeforeReturning { get; init; }
        internal bool WasScheduled => callback is not null;
        internal Task Operation => WasScheduled ? completion.Task : Task.CompletedTask;
        internal Task Schedule(Action action)
        {
            Assert.Null(callback);
            callback = action;
            if (InvokeCallbackBeforeReturning)
            {
                InvokeCallback();
            }

            AfterScheduling?.Invoke();
            return completion.Task;
        }
        internal void Execute()
        {
            if (callback is null)
            {
                return;
            }

            InvokeCallback();
            if (callbackFailure is not null)
            {
                completion.TrySetException(callbackFailure);
            }
            else
            {
                completion.TrySetResult();
            }
        }
        private void InvokeCallback()
        {
            if (callbackInvoked)
            {
                return;
            }

            callbackInvoked = true;
            try { callback!(); }
            catch (Exception failure) { callbackFailure = failure; }
        }
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

    private FixtureProcessException RunFailure(
        SyntheticProcess process,
        FixtureProcessRunner? runner = null,
        Action<string>? followUpWriter = null)
    {
        var ownedRunner = runner ?? new FixtureProcessRunner();
        var failure = Assert.Throws<FixtureProcessException>(() => followUpWriter is null
            ? ownedRunner.Run(() => process)
            : ownedRunner.RunWithFailureFollowUp(() => process, followUpWriter));
        Assert.Equal(failure.Result.Describe(), failure.Message);
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
