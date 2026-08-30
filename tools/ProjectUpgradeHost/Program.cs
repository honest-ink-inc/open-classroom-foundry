// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.ProjectUpgradeHost;

return await ProjectUpgradeOperatorHost.RunAsync(
    args,
    Console.Out,
    Console.Error,
    CancellationToken.None);
