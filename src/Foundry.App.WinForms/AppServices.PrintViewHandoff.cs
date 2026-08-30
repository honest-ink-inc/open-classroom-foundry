// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Foundry.App.WinForms;

public static partial class AppServices
{
    private const string PrintViewHandoffMode = "--serve-approved-print-view";
    private const int MaxPrintViewHandoffBytes = 32 * 1024 * 1024;
    private const int MaxPrintViewRequestHeaderBytes = 16 * 1024;
    internal const int MaxActivePrintViewHelpers = 8;
    private static readonly TimeSpan PrintViewHelperReadyTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PrintViewHelperServeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PrintViewParentResponseTimeout = TimeSpan.FromSeconds(12);
    private static readonly SemaphoreSlim PrintViewHelperSlots =
        new(MaxActivePrintViewHelpers, MaxActivePrintViewHelpers);

    /// <summary>
    /// Starts a bounded copy-owning loopback helper and returns its one-time URL.
    /// The helper validates and copies the file while the caller still owns the
    /// immutable lease, so the parent may remove the pathname immediately after
    /// shell dispatch without racing a reused browser process.
    /// </summary>
    internal static PrintViewHandoff StartPrintViewHandoff(
        string path,
        ReadOnlyMemory<byte> approvedBytes,
        bool launchBrowser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (approvedBytes.IsEmpty || approvedBytes.Length > MaxPrintViewHandoffBytes)
        {
            throw new IOException("print-view.handoff-size");
        }

        if (!PrintViewHelperSlots.Wait(0))
        {
            throw new IOException("print-view.helper-limit");
        }

        var slotHeld = true;
        Process? helper = null;
        try
        {
            var executable = Path.ChangeExtension(typeof(AppServices).Assembly.Location, ".exe");
            if (!File.Exists(executable))
            {
                throw new IOException("print-view.helper-missing");
            }

            var expectedHash = Convert.ToHexString(SHA256.HashData(approvedBytes.Span));
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
            };
            start.ArgumentList.Add(PrintViewHandoffMode);
            start.ArgumentList.Add(Path.GetFullPath(path));
            start.ArgumentList.Add(expectedHash);
            start.ArgumentList.Add(launchBrowser ? "launch" : "wait");

            helper = Process.Start(start)
                ?? throw new IOException("print-view.helper-start-failed");

            var readyLine = helper.StandardOutput
                .ReadLineAsync()
                .WaitAsync(PrintViewHelperReadyTimeout)
                .GetAwaiter()
                .GetResult();
            if (readyLine is null
                || !readyLine.StartsWith("READY ", StringComparison.Ordinal)
                || !Uri.TryCreate(readyLine[6..], UriKind.Absolute, out var url)
                || !string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || !string.Equals(url.Host, IPAddress.Loopback.ToString(), StringComparison.Ordinal)
                || url.Port is <= 0 or > ushort.MaxValue
                || url.AbsolutePath.Length != 34
                || url.AbsolutePath[0] != '/'
                || url.AbsolutePath[^1] != '/'
                || !IsLowerHexToken(url.AbsolutePath[1..^1])
                || !string.IsNullOrEmpty(url.Query)
                || !string.IsNullOrEmpty(url.UserInfo))
            {
                throw new IOException("print-view.helper-ready-invalid");
            }

            var handoff = new PrintViewHandoff(helper, url.AbsoluteUri);
            helper = null;
            slotHeld = false;
            return handoff;
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or TimeoutException
            or NotSupportedException
            or System.ComponentModel.Win32Exception)
        {
            if (helper is not null)
            {
                if (!StopPrintViewHelper(helper))
                {
                    ReleasePrintViewHelperSlotAfterExit(helper);
                    slotHeld = false;
                }
            }

            if (slotHeld)
            {
                PrintViewHelperSlots.Release();
            }

            throw new IOException("print-view.helper-unavailable", failure);
        }
    }

    /// <summary>
    /// Handles the private child-process mode before WinForms or ordinary app
    /// startup. It never prints paths, content, or exception messages; success
    /// emits only the random loopback URL read by the parent process.
    /// </summary>
    internal static bool TryRunPrintViewHandoff(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0
            || !string.Equals(args[0], PrintViewHandoffMode, StringComparison.Ordinal))
        {
            return false;
        }

        Environment.ExitCode = 3;
        if (args.Length != 4
            || args[3] is not ("launch" or "wait"))
        {
            return true;
        }

        byte[]? content = null;
        try
        {
            content = ReadPrintViewHandoffBytes(args[1], args[2]);
            if (ServePrintViewOnce(
                content,
                Guid.NewGuid().ToString("N"),
                launchBrowser: string.Equals(args[3], "launch", StringComparison.Ordinal)))
            {
                Environment.ExitCode = 0;
            }
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or OverflowException
            or SocketException
            or System.ComponentModel.Win32Exception
            or NotSupportedException
            or OperationCanceledException
            or TimeoutException)
        {
            // Content-free child failure: the parent observes EOF/non-readiness
            // and renders the localized refusal on the owning authoring surface.
        }
        finally
        {
            if (content is not null)
            {
                CryptographicOperations.ZeroMemory(content);
            }
        }

        return true;
    }

    private static byte[] ReadPrintViewHandoffBytes(string path, string expectedHashText)
    {
        if (expectedHashText.Length != 64
            || !expectedHashText.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("print-view.helper-hash-invalid");
        }

        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var resolvedPath = Path.GetFullPath(path);
        var jobDirectory = Path.GetDirectoryName(resolvedPath);
        if (jobDirectory is null
            || !IsDirectChild(temporaryRoot, jobDirectory)
            || !IsOwnedPrintViewJobName(Path.GetFileName(jobDirectory))
            || !IsDirectChild(jobDirectory, resolvedPath)
            || !resolvedPath.EndsWith(".print.html", StringComparison.OrdinalIgnoreCase)
            || (File.GetAttributes(resolvedPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("print-view.helper-path-invalid");
        }

        using var rootHandle = OpenDirectoryHandle(temporaryRoot);
        var physicalRoot = GetFinalPath(rootHandle);
        using var stream = new FileStream(
            resolvedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        var physicalPath = GetFinalPath(stream.SafeFileHandle);
        var physicalDirectory = Path.GetDirectoryName(physicalPath);
        var expectedPhysicalDirectory = Path.Combine(physicalRoot, Path.GetFileName(jobDirectory));
        if (physicalDirectory is null
            || !IsDirectChild(physicalRoot, physicalDirectory)
            || !PathsEqual(expectedPhysicalDirectory, physicalDirectory)
            || !IsDirectChild(physicalDirectory, physicalPath)
            || (File.GetAttributes(resolvedPath) & FileAttributes.ReparsePoint) != 0
            || stream.Length is <= 0 or > MaxPrintViewHandoffBytes)
        {
            throw new IOException("print-view.helper-physical-path-invalid");
        }

        var content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        try
        {
            stream.ReadExactly(content);
            var expectedHash = Convert.FromHexString(expectedHashText);
            var actualHash = SHA256.HashData(content);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                {
                    throw new IOException("print-view.helper-content-mismatch");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualHash);
                CryptographicOperations.ZeroMemory(expectedHash);
            }

            return content;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(content);
            throw;
        }
    }

    private static bool ServePrintViewOnce(
        byte[] content,
        string token,
        bool launchBrowser)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        using var deadline = new CancellationTokenSource(PrintViewHelperServeTimeout);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start(backlog: MaxActivePrintViewHelpers);
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var url = FormattableString.Invariant(
            $"http://{IPAddress.Loopback}:{endpoint.Port}/{token}/");
        Console.Out.WriteLine($"READY {url}");
        Console.Out.Flush();

        if (launchBrowser)
        {
            LaunchPrintViewUrlInHelper(url, deadline.Token);
        }

        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            using var client = listener.AcceptTcpClientAsync(deadline.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            try
            {
                if (TryServePrintViewRequest(client, content, token, deadline.Token))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure) when (failure is IOException or SocketException)
            {
                // A browser may probe or abandon the private endpoint before
                // issuing the real request. One nuisance connection cannot
                // consume the one-time handoff or extend its absolute deadline.
            }
        }
    }

    private static bool TryServePrintViewRequest(
        TcpClient client,
        byte[] content,
        string token,
        CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        var header = new byte[MaxPrintViewRequestHeaderBytes];
        var length = 0;
        while (length < header.Length)
        {
            var read = stream.ReadAsync(
                    header.AsMemory(length, header.Length - length),
                    cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (read == 0)
            {
                return false;
            }

            length += read;
            var headerEnd = FindHttpHeaderEnd(header.AsSpan(0, length));
            if (headerEnd < 0)
            {
                continue;
            }

            var firstLineEnd = header.AsSpan(0, headerEnd).IndexOf("\r\n"u8);
            if (firstLineEnd < 0)
            {
                return false;
            }

            var firstLine = Encoding.ASCII.GetString(header, 0, firstLineEnd);
            if (!string.Equals(firstLine, $"GET /{token}/ HTTP/1.1", StringComparison.Ordinal))
            {
                WriteHttpNotFound(stream, cancellationToken);
                return false;
            }

            var responseHeader = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + $"Content-Length: {content.Length.ToString(CultureInfo.InvariantCulture)}\r\n"
                + "Cache-Control: no-store\r\n"
                + "Pragma: no-cache\r\n"
                + "X-Content-Type-Options: nosniff\r\n"
                + "Content-Security-Policy: default-src 'none'; img-src data:; style-src 'unsafe-inline'\r\n"
                + "Connection: close\r\n\r\n");
            stream.WriteAsync(responseHeader, cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            stream.WriteAsync(content, cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            stream.FlushAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();
            return true;
        }

        return false;
    }

    private static int FindHttpHeaderEnd(ReadOnlySpan<byte> header)
        => header.IndexOf("\r\n\r\n"u8);

    private static void WriteHttpNotFound(Stream stream, CancellationToken cancellationToken)
    {
        var response = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray();
        stream.WriteAsync(response, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        stream.FlushAsync(cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    private static bool IsLowerHexToken(string token)
        => token.Length == 32
            && token.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static void LaunchPrintViewUrlInHelper(
        string url,
        CancellationToken cancellationToken,
        Func<ProcessStartInfo, Process?>? launcher = null)
    {
        var start = new ProcessStartInfo(url) { UseShellExecute = true };
        var launchWork = Task.Run(() => (launcher ?? Process.Start)(start));
        try
        {
            using var process = launchWork
                .WaitAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            _ = launchWork.ContinueWith(
                static completed =>
                {
                    if (completed.IsCompletedSuccessfully)
                    {
                        completed.Result?.Dispose();
                    }
                    else
                    {
                        _ = completed.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
    }

    private static bool StopPrintViewHelper(Process helper)
    {
        try
        {
            if (!helper.HasExited)
            {
                // The helper may have launched the user's existing browser.
                // Kill only this disposable boundary process, never its tree.
                helper.Kill(entireProcessTree: false);
                if (!helper.WaitForExit(2000))
                {
                    return false;
                }
            }

            helper.Dispose();
            return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or NotSupportedException)
        {
            // Do not release capacity merely because signalling failed. The
            // process wrapper stays owned until exit is positively observed.
            return false;
        }
    }

    private static void ReleasePrintViewHelperSlotAfterExit(Process helper)
    {
        _ = Task.Run(() =>
        {
            var exited = false;
            try
            {
                exited = helper.WaitForExit(
                    checked((int)PrintViewParentResponseTimeout.TotalMilliseconds));
            }
            catch (Exception failure) when (failure is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
            {
                // An unconfirmed helper retains its slot fail closed.
            }

            if (!exited)
            {
                return;
            }

            helper.Dispose();
            PrintViewHelperSlots.Release();
        });
    }

    internal sealed class PrintViewHandoff : IDisposable
    {
        private Process? _helper;
        private int _disposed;

        internal PrintViewHandoff(Process helper, string url)
        {
            _helper = helper;
            Url = url;
        }

        internal string Url { get; }

        internal void WaitForResponseWrite()
        {
            var helper = Volatile.Read(ref _helper)
                ?? throw new ObjectDisposedException(nameof(PrintViewHandoff));
            try
            {
                if (!helper.WaitForExit(
                    checked((int)PrintViewParentResponseTimeout.TotalMilliseconds)))
                {
                    throw new IOException("print-view.helper-response-timeout");
                }

                if (helper.ExitCode != 0)
                {
                    throw new IOException("print-view.helper-response-unconfirmed");
                }
            }
            catch (Exception failure) when (failure is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
            {
                throw new IOException("print-view.helper-response-unconfirmed", failure);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            var helper = Interlocked.Exchange(ref _helper, null);
            if (helper is null)
            {
                PrintViewHelperSlots.Release();
            }
            else if (StopPrintViewHelper(helper))
            {
                PrintViewHelperSlots.Release();
            }
            else
            {
                ReleasePrintViewHelperSlotAfterExit(helper);
            }

            GC.SuppressFinalize(this);
        }
    }
}
