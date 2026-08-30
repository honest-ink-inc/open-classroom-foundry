// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Foundry.App.WinForms;
using Foundry.Domain;

namespace Foundry.Tests.UiAutomation;

public sealed partial class PrintViewPathTests : IDisposable
{
    private static readonly byte[] ApprovedBytes =
        Encoding.UTF8.GetBytes("<html><body>synthetic approved print view</body></html>");

    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
    private readonly string _outsideRoot = Path.Combine(
        Path.GetTempPath(), "ocf-tests-outside", Guid.NewGuid().ToString("N"));
    private readonly List<string> _reparsePoints = [];

    public void Dispose()
    {
        foreach (var reparsePoint in _reparsePoints)
        {
            TryDeleteReparsePoint(reparsePoint);
        }

        try
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }

        try
        {
            if (Directory.Exists(_outsideRoot))
            {
                Directory.Delete(_outsideRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }

        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("..\\..\\outside")]
    [InlineData("../../outside")]
    [InlineData("C:\\outside")]
    [InlineData("\\\\server\\share\\outside")]
    [InlineData("...")]
    public void Untrusted_names_are_stems_and_cannot_escape_the_print_view_root(string name)
    {
        using var view = AppServices.CreatePrintViewLease(name, _temporaryRoot, ApprovedBytes);
        var path = view.Path;
        var root = Path.GetFullPath(_temporaryRoot);
        var relative = Path.GetRelativePath(root, path);

        Assert.False(Path.IsPathRooted(relative));
        Assert.DoesNotContain(
            relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            segment => segment == "..");
        Assert.EndsWith(".print.html", path, StringComparison.Ordinal);
        Assert.DoesNotContain("..", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.StartsWith("view", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.Equal(ApprovedBytes, ReadLeasedBytes(path));
    }

    [Fact]
    public void Each_print_view_gets_a_unique_job_directory_and_a_bounded_leaf_name()
    {
        var longButAdmitted = new string('a', 256);

        using var first = AppServices.CreatePrintViewLease(
            longButAdmitted,
            _temporaryRoot,
            ApprovedBytes);
        using var second = AppServices.CreatePrintViewLease(
            longButAdmitted,
            _temporaryRoot,
            ApprovedBytes);

        Assert.NotEqual(first.JobDirectory, second.JobDirectory);
        Assert.Equal(64 + ".print.html".Length, Path.GetFileName(first.Path).Length);
        Assert.Equal(Path.GetFileName(first.Path), Path.GetFileName(second.Path));
        Assert.True(Directory.Exists(first.JobDirectory));
        Assert.True(Directory.Exists(second.JobDirectory));
    }

    [Fact]
    public void An_active_old_job_is_not_cleaned_while_its_handle_is_leased()
    {
        using var active = AppServices.CreatePrintViewLease(
            "active-old",
            _temporaryRoot,
            ApprovedBytes);
        Directory.SetLastWriteTimeUtc(active.JobDirectory, DateTime.UtcNow.AddDays(-2));

        AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.FromDays(1));

        Assert.True(Directory.Exists(active.JobDirectory));
        Assert.Equal(ApprovedBytes, ReadLeasedBytes(active.Path));
    }

    [Fact]
    public void A_stale_inactive_job_is_cleaned_after_its_lease_is_released()
    {
        var stale = AppServices.CreatePrintViewLease("stale", _temporaryRoot, ApprovedBytes);
        var staleDirectory = stale.JobDirectory;
        stale.Dispose();
        Directory.SetLastWriteTimeUtc(staleDirectory, DateTime.UtcNow.AddDays(-2));

        AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.FromDays(1));

        Assert.False(Directory.Exists(staleDirectory));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void Windows_device_names_become_ordinary_prefixed_files(string name)
    {
        using var view = AppServices.CreatePrintViewLease(name, _temporaryRoot, ApprovedBytes);
        var path = view.Path;

        Assert.True(File.Exists(path));
        Assert.Equal(ApprovedBytes, ReadLeasedBytes(path));
        Assert.StartsWith("view-", Path.GetFileName(path), StringComparison.Ordinal);
    }

    [Fact]
    public void An_active_lease_denies_content_mutation_replacement_deletion_and_job_displacement()
    {
        using var view = AppServices.CreatePrintViewLease(
            "immutable",
            _temporaryRoot,
            ApprovedBytes);
        var substitute = Path.Combine(_temporaryRoot, "synthetic-substitute.html");
        var displaced = $"{view.JobDirectory}-displaced";
        File.WriteAllText(substitute, "synthetic substituted content");

        AssertFilesystemRefusal(() =>
            File.WriteAllText(view.Path, "synthetic mutated content"));
        AssertFilesystemRefusal(() => File.Delete(view.Path));
        AssertFilesystemRefusal(() => File.Move(substitute, view.Path, overwrite: true));
        AssertFilesystemRefusal(() => Directory.Move(view.JobDirectory, displaced));

        Assert.Equal(ApprovedBytes, ReadLeasedBytes(view.Path));
        Assert.Equal("synthetic substituted content", File.ReadAllText(substitute));
        Assert.False(Directory.Exists(displaced));
    }

    [Fact]
    public void Active_print_view_handles_are_capped_without_retiring_an_unacknowledged_browser_handoff()
    {
        var leases = new List<AppServices.PrintViewLease>();
        try
        {
            for (var index = 0; index < AppServices.MaxActivePrintViewLeases; index++)
            {
                leases.Add(AppServices.CreatePrintViewLease(
                    $"bounded-{index}",
                    _temporaryRoot,
                    ApprovedBytes));
            }

            var refusal = Assert.Throws<IOException>(() =>
                AppServices.CreatePrintViewLease(
                    "bounded-refused",
                    _temporaryRoot,
                    ApprovedBytes));

            Assert.Equal("print-view.active-limit", refusal.Message);
            Assert.Equal(
                AppServices.MaxActivePrintViewLeases,
                Directory.EnumerateDirectories(
                    _temporaryRoot,
                    $"{EngineIdentity.InternalId}-print-view-*",
                    SearchOption.TopDirectoryOnly).Count());
            Assert.All(leases, lease =>
            {
                Assert.True(Directory.Exists(lease.JobDirectory));
                Assert.Equal(ApprovedBytes, ReadLeasedBytes(lease.Path));
            });
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }

            AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.Zero);
        }
    }

    [Fact]
    public void Concurrent_print_view_creation_never_exceeds_the_live_handle_cap()
    {
        var leases = new ConcurrentBag<AppServices.PrintViewLease>();
        var failures = new ConcurrentBag<Exception>();
        try
        {
            Parallel.For(0, 24, index =>
            {
                try
                {
                    leases.Add(AppServices.CreatePrintViewLease(
                        $"parallel-{index}",
                        _temporaryRoot,
                        ApprovedBytes));
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                }
            });

            Assert.Equal(AppServices.MaxActivePrintViewLeases, leases.Count);
            Assert.Equal(24 - AppServices.MaxActivePrintViewLeases, failures.Count);
            Assert.All(failures, failure => Assert.IsType<IOException>(failure));
            Assert.Equal(
                AppServices.MaxActivePrintViewLeases,
                Directory.EnumerateDirectories(
                    _temporaryRoot,
                    $"{EngineIdentity.InternalId}-print-view-*",
                    SearchOption.TopDirectoryOnly).Count());
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }

            AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.Zero);
        }
    }

    [Fact]
    public void Content_replaced_between_exclusive_write_and_read_lease_is_refused_after_revalidation()
    {
        var substituted = Encoding.UTF8.GetBytes("synthetic substituted content");
        string? path = null;

        var failure = Assert.Throws<IOException>(() =>
            AppServices.CreatePrintViewLease(
                "replaced-before-lease",
                _temporaryRoot,
                ApprovedBytes,
                contentWritten: candidate =>
                {
                    path = candidate;
                    File.Delete(candidate);
                    File.WriteAllBytes(candidate, substituted);
                }));

        Assert.Equal("print-view.content-changed-before-lease", failure.Message);
        Assert.False(Directory.Exists(Path.GetDirectoryName(Assert.IsType<string>(path))));
    }

    [Fact]
    public void A_transient_quarantine_validation_failure_is_retried_by_the_next_sweep()
    {
        var stale = AppServices.CreatePrintViewLease("quarantine-retry", _temporaryRoot, ApprovedBytes);
        var jobDirectory = stale.JobDirectory;
        stale.Dispose();
        string? quarantine = null;
        string? blocker = null;

        AppServices.CleanupPrintViewJobs(
            _temporaryRoot,
            TimeSpan.Zero,
            quarantineReady: path =>
            {
                quarantine = path;
                blocker = Path.Combine(path, "synthetic-blocker.txt");
                File.WriteAllText(blocker, "synthetic transient blocker");
            });

        Assert.False(Directory.Exists(jobDirectory));
        Assert.True(Directory.Exists(Assert.IsType<string>(quarantine)));
        File.Delete(Assert.IsType<string>(blocker));

        AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.Zero);

        Assert.False(Directory.Exists(quarantine));
    }

    [Fact]
    public void Process_shutdown_release_closes_and_removes_every_registered_print_view_job()
    {
        var first = AppServices.CreatePrintViewLease("shutdown-one", _temporaryRoot, ApprovedBytes);
        var second = AppServices.CreatePrintViewLease("shutdown-two", _temporaryRoot, ApprovedBytes);
        var firstDirectory = first.JobDirectory;
        var secondDirectory = second.JobDirectory;

        AppServices.ReleaseAllPrintViewLeases();

        Assert.False(Directory.Exists(firstDirectory));
        Assert.False(Directory.Exists(secondDirectory));
        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task Loopback_handoff_serves_the_exact_owned_bytes_after_the_parent_path_is_deleted()
    {
        var lease = AppServices.CreatePrintViewLease(
            "loopback-handoff",
            Path.GetTempPath(),
            ApprovedBytes);
        var path = lease.Path;
        var jobDirectory = lease.JobDirectory;
        try
        {
            using var handoff = AppServices.StartPrintViewHandoff(
                path,
                ApprovedBytes,
                launchBrowser: false);
            lease.Dispose();
            File.Delete(path);
            Directory.Delete(jobDirectory, recursive: false);
            Assert.False(File.Exists(path));

            var endpoint = new Uri(handoff.Url);
            using (var nuisance = new TcpClient(AddressFamily.InterNetwork))
            {
                await nuisance.ConnectAsync(endpoint.Host, endpoint.Port);
                await nuisance.GetStream().WriteAsync("G"u8.ToArray());
                nuisance.Client.LingerState = new LingerOption(enable: true, seconds: 0);
                await Task.Delay(100);
            }

            using var handler = new SocketsHttpHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            var received = await client.GetByteArrayAsync(handoff.Url);
            handoff.WaitForResponseWrite();

            Assert.Equal(ApprovedBytes, received);
            Assert.False(File.Exists(path));
        }
        finally
        {
            lease.Dispose();
            try
            {
                File.Delete(path);
                if (Directory.Exists(jobDirectory))
                {
                    Directory.Delete(jobDirectory, recursive: false);
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // Exact synthetic job cleanup is best effort after a failed proof.
            }
        }
    }

    [Fact]
    public void Loopback_helpers_are_capped_and_a_ninth_cannot_retire_an_unanswered_handoff()
    {
        var lease = AppServices.CreatePrintViewLease(
            "loopback-cap",
            Path.GetTempPath(),
            ApprovedBytes);
        var handoffs = new List<AppServices.PrintViewHandoff>();
        try
        {
            for (var index = 0; index < AppServices.MaxActivePrintViewHelpers; index++)
            {
                handoffs.Add(AppServices.StartPrintViewHandoff(
                    lease.Path,
                    ApprovedBytes,
                    launchBrowser: false));
            }

            var refusal = Assert.Throws<IOException>(() =>
                AppServices.StartPrintViewHandoff(
                    lease.Path,
                    ApprovedBytes,
                    launchBrowser: false));

            Assert.Equal("print-view.helper-limit", refusal.Message);
        }
        finally
        {
            foreach (var handoff in handoffs)
            {
                handoff.Dispose();
            }

            lease.Dispose();
            AppServices.CleanupPrintViewJobs(Path.GetTempPath(), TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task A_slow_client_cannot_extend_the_helpers_absolute_lifetime()
    {
        var lease = AppServices.CreatePrintViewLease(
            "loopback-slow-client",
            Path.GetTempPath(),
            ApprovedBytes);
        var path = lease.Path;
        var jobDirectory = lease.JobDirectory;
        TcpClient? slowClient = null;
        try
        {
            using var handoff = AppServices.StartPrintViewHandoff(
                path,
                ApprovedBytes,
                launchBrowser: false);
            lease.Dispose();
            File.Delete(path);
            Directory.Delete(jobDirectory, recursive: false);

            var endpoint = new Uri(handoff.Url);
            slowClient = new TcpClient(AddressFamily.InterNetwork);
            await slowClient.ConnectAsync(endpoint.Host, endpoint.Port);
            var clientForWriter = slowClient;
            var slowWriter = Task.Run(async () =>
            {
                try
                {
                    var stream = clientForWriter.GetStream();
                    for (var index = 0; index < 60; index++)
                    {
                        await stream.WriteAsync("G"u8.ToArray());
                        await stream.FlushAsync();
                        await Task.Delay(250);
                    }
                }
                catch (Exception failure) when (failure is IOException
                    or SocketException
                    or ObjectDisposedException)
                {
                    // Expected when the absolute child deadline closes the socket.
                }
            });

            var elapsed = Stopwatch.StartNew();
            var refusal = Assert.Throws<IOException>(handoff.WaitForResponseWrite);
            elapsed.Stop();

            Assert.Equal("print-view.helper-response-unconfirmed", refusal.Message);
            Assert.True(
                elapsed.Elapsed < TimeSpan.FromSeconds(11.5),
                $"Slow client kept the helper alive for {elapsed.Elapsed}.");

            slowClient.Dispose();
            slowClient = null;
            await slowWriter.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            slowClient?.Dispose();
            lease.Dispose();
            try
            {
                File.Delete(path);
                if (Directory.Exists(jobDirectory))
                {
                    Directory.Delete(jobDirectory, recursive: false);
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // Exact synthetic job cleanup is best effort after a failed proof.
            }
        }
    }

    [Fact]
    public async Task A_blocked_shell_dispatch_is_bounded_inside_the_helper_process()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        using var finished = new ManualResetEventSlim(initialState: false);
        using var deadline = new CancellationTokenSource();
        var cancelWork = Task.Run(() =>
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            deadline.Cancel();
        });

        try
        {
            var elapsed = Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                AppServices.LaunchPrintViewUrlInHelper(
                    "http://127.0.0.1:1/synthetic/",
                    deadline.Token,
                    start =>
                    {
                        try
                        {
                            Assert.True(start.UseShellExecute);
                            entered.Set();
                            release.Wait();
                            return null;
                        }
                        finally
                        {
                            finished.Set();
                        }
                    }));
            elapsed.Stop();

            Assert.True(
                elapsed.Elapsed < TimeSpan.FromSeconds(3),
                $"Blocked shell dispatch held the helper for {elapsed.Elapsed}.");
        }
        finally
        {
            release.Set();
            await cancelWork;
            Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        }
    }

    [Fact]
    public void A_preexisting_leaf_is_never_overwritten_with_approved_bytes()
    {
        var sentinel = Encoding.UTF8.GetBytes("synthetic pre-existing leaf");
        string? contenderPath = null;

        Assert.Throws<IOException>(() =>
        {
            _ = AppServices.CreatePrintViewLease(
                "preexisting",
                _temporaryRoot,
                ApprovedBytes,
                path =>
                {
                    contenderPath = path;
                    File.WriteAllBytes(path, sentinel);
                });
        });

        var path = Assert.IsType<string>(contenderPath);
        Assert.Equal(sentinel, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_preexisting_hardlink_is_never_opened_or_overwritten()
    {
        Directory.CreateDirectory(_outsideRoot);
        var target = Path.Combine(_outsideRoot, "hardlink-target.html");
        var sentinel = Encoding.UTF8.GetBytes("synthetic hardlink target");
        File.WriteAllBytes(target, sentinel);
        string? contenderPath = null;

        Assert.Throws<IOException>(() =>
        {
            _ = AppServices.CreatePrintViewLease(
                "hardlink",
                _temporaryRoot,
                ApprovedBytes,
                path =>
                {
                    contenderPath = path;
                    if (!CreateHardLink(path, target, securityAttributes: 0))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                });
        });

        var link = Assert.IsType<string>(contenderPath);
        Assert.Equal(sentinel, File.ReadAllBytes(target));
        Assert.Equal(sentinel, File.ReadAllBytes(link));
    }

    [Fact]
    public void A_preexisting_symlink_is_never_followed_or_overwritten()
    {
        Directory.CreateDirectory(_outsideRoot);
        var target = Path.Combine(_outsideRoot, "symlink-target.html");
        var sentinel = Encoding.UTF8.GetBytes("synthetic symlink target");
        File.WriteAllBytes(target, sentinel);
        string? contenderPath = null;

        Assert.Throws<IOException>(() =>
        {
            _ = AppServices.CreatePrintViewLease(
                "symlink",
                _temporaryRoot,
                ApprovedBytes,
                path =>
                {
                    contenderPath = path;
                    File.CreateSymbolicLink(path, target);
                    _reparsePoints.Add(path);
                });
        });

        var link = Assert.IsType<string>(contenderPath);
        Assert.Equal(sentinel, File.ReadAllBytes(target));
        Assert.Equal(sentinel, File.ReadAllBytes(link));
    }

    [Fact]
    public void A_job_directory_redirected_outside_the_root_gets_no_approved_bytes()
    {
        Directory.CreateDirectory(_outsideRoot);
        var nestedOutside = Path.Combine(_outsideRoot, "nested");
        Directory.CreateDirectory(nestedOutside);
        var sentinel = Path.Combine(nestedOutside, "synthetic-sentinel.txt");
        File.WriteAllText(sentinel, "do not traverse");
        string? redirectedJob = null;

        var failure = Assert.Throws<IOException>(() =>
        {
            _ = AppServices.CreatePrintViewLease(
                "redirected",
                _temporaryRoot,
                ApprovedBytes,
                path =>
                {
                    redirectedJob = Path.GetDirectoryName(path)!;
                    Directory.Delete(redirectedJob, recursive: false);
                    Directory.CreateSymbolicLink(redirectedJob, _outsideRoot);
                });
        });

        Assert.Equal("print-view.physical-job-mismatch", failure.Message);
        var quarantinedLink = FindSingleReparseDirectory(_temporaryRoot);
        _reparsePoints.Add(quarantinedLink);
        var escaped = Assert.Single(Directory.EnumerateFiles(_outsideRoot));
        Assert.Equal(0, new FileInfo(escaped).Length);

        AppServices.CleanupPrintViewJobs(_temporaryRoot, TimeSpan.Zero);

        Assert.False(Directory.Exists(Assert.IsType<string>(redirectedJob)));
        Assert.True(Directory.Exists(quarantinedLink));
        Assert.Equal("do not traverse", File.ReadAllText(sentinel));
        Assert.Equal(0, new FileInfo(escaped).Length);
    }

    [Fact]
    public void A_job_directory_redirected_within_the_root_is_still_refused()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var unrelatedTarget = Path.Combine(_temporaryRoot, "unrelated-target");
        Directory.CreateDirectory(unrelatedTarget);
        var sentinel = Path.Combine(unrelatedTarget, "synthetic-sentinel.txt");
        File.WriteAllText(sentinel, "do not inject here");

        var failure = Assert.Throws<IOException>(() =>
        {
            _ = AppServices.CreatePrintViewLease(
                "redirected-inside",
                _temporaryRoot,
                ApprovedBytes,
                path =>
                {
                    var jobDirectory = Path.GetDirectoryName(path)!;
                    Directory.Delete(jobDirectory, recursive: false);
                    Directory.CreateSymbolicLink(jobDirectory, unrelatedTarget);
                });
        });

        Assert.Equal("print-view.physical-job-mismatch", failure.Message);
        var quarantinedLink = FindSingleReparseDirectory(_temporaryRoot);
        _reparsePoints.Add(quarantinedLink);
        var emptyPrintView = Assert.Single(
            Directory.EnumerateFiles(unrelatedTarget, "*.print.html"));
        Assert.Equal(0, new FileInfo(emptyPrintView).Length);
        Assert.Equal("do not inject here", File.ReadAllText(sentinel));
    }

    [Fact]
    public void Cleanup_quarantines_a_raced_directory_redirect_before_inspecting_files()
    {
        Directory.CreateDirectory(_outsideRoot);
        var outsidePrintFile = Path.Combine(_outsideRoot, "synthetic-victim.print.html");
        File.WriteAllText(outsidePrintFile, "do not delete through a link");
        var stale = AppServices.CreatePrintViewLease(
            "cleanup-race",
            _temporaryRoot,
            ApprovedBytes);
        var staleDirectory = stale.JobDirectory;
        var stalePath = stale.Path;
        stale.Dispose();
        Directory.SetLastWriteTimeUtc(staleDirectory, DateTime.UtcNow.AddDays(-2));
        var callbackCount = 0;

        AppServices.CleanupPrintViewJobs(
            _temporaryRoot,
            TimeSpan.FromDays(1),
            path =>
            {
                callbackCount++;
                Assert.Equal(staleDirectory, path);
                File.Delete(stalePath);
                Directory.Delete(staleDirectory, recursive: false);
                Directory.CreateSymbolicLink(staleDirectory, _outsideRoot);
            });

        Assert.Equal(1, callbackCount);
        Assert.False(Directory.Exists(staleDirectory));
        var quarantinedLink = FindSingleReparseDirectory(_temporaryRoot);
        _reparsePoints.Add(quarantinedLink);
        Assert.True(Directory.Exists(quarantinedLink));
        Assert.Equal("do not delete through a link", File.ReadAllText(outsidePrintFile));
    }

    [Fact]
    public void The_old_stable_print_view_subtree_is_never_used_as_a_job_parent()
    {
        var stableSubtree = Path.Combine(
            _temporaryRoot,
            EngineIdentity.InternalId,
            "print-view");
        Directory.CreateDirectory(stableSubtree);
        File.WriteAllText(Path.Combine(stableSubtree, "sentinel.txt"), "do not traverse");

        using var view = AppServices.CreatePrintViewLease(
            "ordinary",
            _temporaryRoot,
            ApprovedBytes);
        var path = view.Path;

        Assert.Equal(Path.GetFullPath(_temporaryRoot), Path.GetDirectoryName(Path.GetDirectoryName(path)));
        Assert.Equal("do not traverse", File.ReadAllText(Path.Combine(stableSubtree, "sentinel.txt")));
    }

    [Fact]
    public void Empty_or_unbounded_names_are_refused_before_any_directory_is_created()
    {
        Assert.Throws<ArgumentException>(() =>
            AppServices.CreatePrintViewLease(" ", _temporaryRoot, ApprovedBytes));
        Assert.Throws<ArgumentException>(() =>
            AppServices.CreatePrintViewLease(new string('a', 257), _temporaryRoot, ApprovedBytes));

        Assert.False(Directory.Exists(_temporaryRoot));
    }

    private static void TryDeleteReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.Delete(path, recursive: false);
            }
            else
            {
                File.Delete(path);
            }
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private static byte[] ReadLeasedBytes(string path) => File.ReadAllBytes(path);

    private static void AssertFilesystemRefusal(Action action)
    {
        var failure = Record.Exception(action);
        Assert.True(
            failure is IOException or UnauthorizedAccessException,
            $"Expected a filesystem refusal; received {failure?.GetType().FullName ?? "no exception"}.");
    }

    private static string FindSingleReparseDirectory(string root)
        => Assert.Single(
            Directory.EnumerateDirectories(root),
            directory => (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(
        string fileName,
        string existingFileName,
        nint securityAttributes);
}
