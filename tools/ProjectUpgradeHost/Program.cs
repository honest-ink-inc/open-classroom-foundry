// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.ProjectUpgradeHost;

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    // Keep the process alive long enough for the compatibility service's
    // all-or-nothing cleanup to run through the supplied token.
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.CancelKeyPress += cancelHandler;

// Subscribing is not enough. An inherited "ignore CTRL+C" flag is consulted before
// any handler runs, so a host launched by a parent that set it would accept the
// signal into a void and finish the batch it was asked to abandon. Sighting S-21.
if (OperatingSystem.IsWindows())
{
    _ = ConsoleCancellation.RestoreInheritedCancellation();
}

try
{
    return await ProjectUpgradeOperatorHost.RunAsync(
        args,
        Console.Out,
        Console.Error,
        cancellation.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
