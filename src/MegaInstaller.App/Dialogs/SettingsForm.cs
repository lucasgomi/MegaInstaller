using System.Reflection;
using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>Where the installers folder (previously on the Home screen) now lives, plus the app's look.</summary>
public sealed class SettingsForm : Form
{
    private readonly TextBox _folderTextBox;
    private readonly RadioButton _classicRadio;
    private readonly RadioButton _modernRadio;
    private readonly CheckBox _troubleshooterCheck;
    private readonly CheckBox _elevationOfferCheck;

    public string SelectedFolder { get; private set; }

    public UiThemeMode SelectedTheme => _modernRadio.Checked ? UiThemeMode.Modern : UiThemeMode.Classic;

    public bool TroubleshooterEnabled => _troubleshooterCheck.Checked;

    public bool SkipElevationOffer => !_elevationOfferCheck.Checked;

    public SettingsForm(string currentFolder, UiThemeMode currentTheme, AppSettings settings)
    {
        SelectedFolder = currentFolder;

        Text = "Ajustes";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 380);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 9 };
        // RowStyles is positional (RowStyles[i] = row i); declare all of
        // them upfront so none fall back to an unpredictable default.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "Carpeta de instaladores:", AutoSize = true }, 0, 0);

        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = currentFolder };
        var browseButton = AppTheme.CreateButton("Examinar...");
        browseButton.Margin = new Padding(6, 0, 0, 0);
        browseButton.Click += OnBrowse;
        var openButton = AppTheme.CreateButton("Abrir carpeta");
        openButton.Margin = new Padding(6, 0, 0, 0);
        openButton.Click += OnOpenFolder;
        folderPanel.Controls.Add(_folderTextBox, 0, 0);
        folderPanel.Controls.Add(browseButton, 1, 0);
        folderPanel.Controls.Add(openButton, 2, 0);
        root.Controls.Add(folderPanel, 0, 1);

        root.Controls.Add(new Label { Text = "Interfaz:", AutoSize = true }, 0, 2);

        var themePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _classicRadio = new RadioButton { Text = "Clásica", AutoSize = true, Checked = currentTheme == UiThemeMode.Classic };
        _modernRadio = new RadioButton { Text = "Moderna (beta)", AutoSize = true, Checked = currentTheme == UiThemeMode.Modern, Margin = new Padding(16, 0, 0, 0) };
        themePanel.Controls.Add(_classicRadio);
        themePanel.Controls.Add(_modernRadio);
        root.Controls.Add(themePanel, 0, 3);

        _troubleshooterCheck = new CheckBox
        {
            Text = "Activar el diagnóstico de instalación (troubleshooter)",
            AutoSize = true,
            Checked = settings.TroubleshooterEnabled,
        };
        root.Controls.Add(_troubleshooterCheck, 0, 4);

        _elevationOfferCheck = new CheckBox
        {
            Text = "Ofrecer reiniciar como administrador para un solo aviso de UAC",
            AutoSize = true,
            Checked = !settings.SkipElevationOffer,
        };
        root.Controls.Add(_elevationOfferCheck, 0, 5);

        var elevateNowButton = AppTheme.CreateButton("Reiniciar como administrador ahora");
        elevateNowButton.Enabled = !ElevationProbe.IsProcessElevated();
        elevateNowButton.Click += (_, _) => ElevatedRelauncher.TryRelaunchElevated(this);
        root.Controls.Add(elevateNowButton, 0, 6);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        root.Controls.Add(new Label
        {
            Text = (version is null ? "MegaInstaller" : $"MegaInstaller v{version.Major}.{version.Minor}") +
                   (ElevationProbe.IsProcessElevated() ? " - ejecutándose como administrador" : string.Empty),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 0),
        }, 0, 7);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = AppTheme.CreateButton("Cerrar");
        closeButton.DialogResult = DialogResult.OK;
        buttonsPanel.Controls.Add(closeButton);
        root.Controls.Add(buttonsPanel, 0, 8);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        AppTheme.StyleForm(this);
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona (o crea) la carpeta donde viven tus instaladores",
            ShowNewFolderButton = true,
            SelectedPath = SelectedFolder,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectedFolder = dialog.SelectedPath;
            _folderTextBox.Text = SelectedFolder;
        }
    }

    private void OnOpenFolder(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            MessageBox.Show(this, "Todavía no hay una carpeta válida seleccionada.", "Sin carpeta",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = SelectedFolder,
            UseShellExecute = true,
        });
    }
}
