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
