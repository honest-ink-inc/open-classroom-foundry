// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        // Fully qualified: the Foundry.Application engine namespace shadows
        // System.Windows.Forms.Application inside the Foundry.* namespace tree.
        System.Windows.Forms.Application.Run(new Form1());
    }
}
