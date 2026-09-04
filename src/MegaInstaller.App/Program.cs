using MegaInstaller.App.Theming;

namespace MegaInstaller.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppTheme.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI", 9F));
        Application.Run(new MainForm());
    }
}
