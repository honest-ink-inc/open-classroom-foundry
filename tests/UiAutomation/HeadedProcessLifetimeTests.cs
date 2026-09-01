// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace Foundry.Tests.UiAutomation;

public sealed class HeadedProcessLifetimeTests
{
    [Fact]
    public void Termination_is_observed_before_the_fixture_releases_its_child()
    {
        using var process = Process.Start(new ProcessStartInfo(
            "cmd.exe",
            "/d /c ping 127.0.0.1 -n 30 > nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;

        HeadedProcessLifetime.TerminateAndWait(process);

        Assert.True(process.HasExited);
    }

    [Fact]
    public void An_already_exited_child_also_satisfies_the_shutdown_contract()
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c exit 0")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        Assert.True(process.WaitForExit(5000));

        HeadedProcessLifetime.TerminateAndWait(process);

        Assert.True(process.HasExited);
    }
}
