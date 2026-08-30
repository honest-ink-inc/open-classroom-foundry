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
        Form? harnessForm;
        try
        {
            if (UiLocale.TryExportTemplate(args))
            {
                return;
            }

            // Locale first: every form reads the catalog in its constructor.
            UiLocale.Configure(args);

            // A managed deployment may select an exact, already prepared
            // version-addressed library. Validation completes before any form
            // can save or open a project.
            _ = ProjectLibraryRootConfiguration.ApplyIfPresent(args);

            // Harness parsing shares this refusal boundary. A legacy test
            // switch must never become a second shipped route around the
            // validated version-addressed library root.
            harnessForm = UiaHarness.FromArgs(args);
        }
        catch (Exception refusal) when (refusal is InvalidDataException or ProjectLibraryRootException)
        {
            _ = MessageBox.Show(refusal.Message, ProductIdentity.PublicName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 2;
            return;
        }

        // Fully qualified: the Foundry.Application engine namespace shadows
        // System.Windows.Forms.Application inside the Foundry.* namespace tree.
        System.Windows.Forms.Application.Run(harnessForm ?? new PressRoomForm());
    }
}
