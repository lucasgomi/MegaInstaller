using MegaInstaller.App.Theming;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Shown once per Windows session, right when the app starts: pick (or
/// confirm) the installers folder before going any further. Choosing
/// "Salir" closes the whole application rather than leaving it half-set-up.
/// </summary>
public sealed class SelectFolderStartupForm : Form
{
    private readonly TextBox _folderTextBox;
    private readonly Button _continueButton;

    public string? SelectedFolder { get; private set; }

    public SelectFolderStartupForm(string? initialFolder)
    {
        Text = "MegaInstaller";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 214);

        var icon = InstanceIconCatalog.Load("box-seam-fill");

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // RowStyles is positional (RowStyles[i] = row i); declare all of
        // them upfront so none fall back to an unpredictable default.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        if (icon is not null)
        {
            var iconBox = new PictureBox { Image = icon, Width = 40, Height = 40, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 12, 12) };
            root.Controls.Add(iconBox, 0, 0);
            root.SetRowSpan(iconBox, 2);
        }

        root.Controls.Add(new Label
        {
            Text = "Bienvenido a MegaInstaller. Elige la carpeta donde tienes (o quieres tener) tus instaladores.",
            AutoSize = true,
            MaximumSize = new Size(390, 0),
        }, 1, 0);

        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = initialFolder ?? string.Empty };
        var browseButton = AppTheme.CreateButton("Examinar...");
        browseButton.Click += OnBrowse;
        folderPanel.Controls.Add(_folderTextBox, 0, 0);
        folderPanel.Controls.Add(browseButton, 1, 0);
        root.Controls.Add(folderPanel, 1, 1);

        root.Controls.Add(new Label
        {
            Text = "Podrás cambiarla luego desde Ajustes.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        }, 1, 2);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var exitButton = AppTheme.CreateButton("Salir");
        exitButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _continueButton = AppTheme.CreateButton("Continuar", primary: true);
        _continueButton.Enabled = !string.IsNullOrWhiteSpace(initialFolder);
        _continueButton.Click += OnContinue;
        buttonsPanel.Controls.Add(exitButton);
        buttonsPanel.Controls.Add(_continueButton);
        root.Controls.Add(buttonsPanel, 1, 3);

        AcceptButton = _continueButton;
        CancelButton = exitButton;

        AppTheme.StyleForm(this);
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona (o crea) la carpeta donde viven tus instaladores",
            ShowNewFolderButton = true,
        };
        if (!string.IsNullOrWhiteSpace(_folderTextBox.Text)) dialog.SelectedPath = _folderTextBox.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderTextBox.Text = dialog.SelectedPath;
            _continueButton.Enabled = true;
        }
    }

    private void OnContinue(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_folderTextBox.Text))
        {
            MessageBox.Show(this, "Selecciona una carpeta para continuar.", "Falta la carpeta",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedFolder = _folderTextBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
