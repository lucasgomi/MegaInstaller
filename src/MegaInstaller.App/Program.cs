using MegaInstaller.App.Dialogs;
using MegaInstaller.App.Theming;

namespace MegaInstaller.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppTheme.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI", 9F));

        // Started by a normal instance to run one batch elevated (a single
        // UAC prompt covers every installer in it); shows just the install
        // window and exits when it closes.
        if (args.Length >= 2 && args[0] == ElevatedInstallLauncher.BatchSwitch)
        {
            RunElevatedBatch(args[1]);
            return;
        }

        Application.Run(new MainForm());
    }

    private static void RunElevatedBatch(string planPath)
    {
        var plan = ElevatedInstallLauncher.ConsumePlan(planPath);
        if (plan is null || plan.Entries.Count == 0)
        {
            MessageBox.Show("No se pudo leer la lista de programas a instalar.", "MegaInstaller",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new InstallProgressForm(plan.Folder, plan.Entries, plan.StopOnError));
    }
}
