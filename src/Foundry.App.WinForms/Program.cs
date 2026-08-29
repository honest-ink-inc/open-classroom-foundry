// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        // Locale first: every form reads the catalog in its constructor.
        UiLocale.Configure(args);
        // Fully qualified: the Foundry.Application engine namespace shadows
        // System.Windows.Forms.Application inside the Foundry.* namespace tree.
        System.Windows.Forms.Application.Run(UiaHarness.FromArgs(args) ?? new PressRoomForm());
    }
}
