using System.Reflection;

namespace MegaInstaller.App.Dialogs;

/// <summary>Where the installers folder (previously on the Home screen) now lives.</summary>
public sealed class SettingsForm : Form
{
    private readonly TextBox _folderTextBox;

    public string SelectedFolder { get; private set; }

    public SettingsForm(string currentFolder)
    {
        SelectedFolder = currentFolder;

        Text = "Ajustes";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 160);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "Carpeta de instaladores:", AutoSize = true }, 0, 0);

        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = currentFolder };
        var browseButton = new Button { Text = "Examinar...", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        browseButton.Click += OnBrowse;
        var openButton = new Button { Text = "Abrir carpeta", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        openButton.Click += OnOpenFolder;
        folderPanel.Controls.Add(_folderTextBox, 0, 0);
        folderPanel.Controls.Add(browseButton, 1, 0);
        folderPanel.Controls.Add(openButton, 2, 0);
        root.Controls.Add(folderPanel, 0, 1);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        root.Controls.Add(new Label
        {
            Text = version is null ? "MegaInstaller" : $"MegaInstaller v{version.Major}.{version.Minor}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 16, 0, 0),
        }, 0, 2);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = new Button { Text = "Cerrar", DialogResult = DialogResult.OK, AutoSize = true };
        buttonsPanel.Controls.Add(closeButton);
        root.Controls.Add(buttonsPanel, 0, 3);

        AcceptButton = closeButton;
        CancelButton = closeButton;
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
