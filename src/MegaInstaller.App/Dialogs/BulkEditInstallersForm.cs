using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Applies the same change to several installers at once, without opening
/// each one individually. Every field is opt-in (a "Cambiar ..." checkbox
/// gates it) so a bulk edit only ever touches what you explicitly ticked -
/// unchecked fields are left exactly as each program already had them.
/// </summary>
public sealed class BulkEditInstallersForm : Form
{
    private readonly List<InstallerEntry> _entries;

    private readonly CheckBox _changeArgumentsCheck;
    private readonly TextBox _argumentsBox;
    private readonly CheckBox _changeInstallDirCheck;
    private readonly TextBox _installDirBox;
    private readonly CheckBox _changeAdminCheck;
    private readonly RadioButton _adminYesRadio;
    private readonly RadioButton _adminNoRadio;
    private readonly CheckBox _changeOrderCheck;
    private readonly NumericUpDown _orderUpDown;
    private readonly CheckBox _addTagsCheck;
    private readonly TextBox _tagsBox;

    public BulkEditInstallersForm(List<InstallerEntry> entries)
    {
        _entries = entries;

        Text = $"Editar {entries.Count} programas";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 320);

        // Column 0 auto-sizes to whichever checkbox label is longest, instead
        // of a fixed width that clipped the longer ones. Rows use explicit
        // Absolute heights rather than AutoSize: an AutoSize row containing a
        // Dock=Fill child (every row here pairs a checkbox with a Dock=Fill
        // textbox/panel) has no well-defined preferred height to measure,
        // which is what made rows render at inconsistent/collapsed heights.
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int[] rowHeights = { 48, 32, 32, 32, 32, 32, 32, 44 };
        foreach (var height in rowHeights)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        Controls.Add(layout);

        var row = 0;

        var headerLabel = new Label
        {
            Text = $"Editando {entries.Count} programa(s). Solo se aplican los campos marcados; el resto se deja como estaba.",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Margin = new Padding(3, 3, 3, 12),
        };
        layout.Controls.Add(headerLabel, 0, row);
        layout.SetColumnSpan(headerLabel, 2);
        row++;

        _changeArgumentsCheck = new CheckBox { Text = "Cambiar argumentos a:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        _argumentsBox = new TextBox { Dock = DockStyle.Fill, Enabled = false, Margin = new Padding(3, 6, 3, 6) };
        _changeArgumentsCheck.CheckedChanged += (_, _) => _argumentsBox.Enabled = _changeArgumentsCheck.Checked;
        layout.Controls.Add(_changeArgumentsCheck, 0, row);
        layout.Controls.Add(_argumentsBox, 1, row);
        row++;

        _changeInstallDirCheck = new CheckBox { Text = "Añadir carpeta destino:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        var installDirPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(3, 6, 3, 6) };
        installDirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        installDirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _installDirBox = new TextBox { Dock = DockStyle.Fill, Enabled = false, Margin = new Padding(0) };
        var browseButton = AppTheme.CreateButton("...");
        browseButton.Enabled = false;
        browseButton.Margin = new Padding(6, 0, 0, 0);
        browseButton.Click += OnBrowseInstallDir;
        _changeInstallDirCheck.CheckedChanged += (_, _) =>
        {
            _installDirBox.Enabled = _changeInstallDirCheck.Checked;
            browseButton.Enabled = _changeInstallDirCheck.Checked;
        };
        installDirPanel.Controls.Add(_installDirBox, 0, 0);
        installDirPanel.Controls.Add(browseButton, 1, 0);
        layout.Controls.Add(_changeInstallDirCheck, 0, row);
        layout.Controls.Add(installDirPanel, 1, row);
        row++;

        layout.Controls.Add(new Label(), 0, row);
        layout.Controls.Add(new Label
        {
            Text = "Solo se aplica a los programas MSI, Inno Setup o NSIS de la selección.",
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 6),
        }, 1, row);
        row++;

        _changeAdminCheck = new CheckBox { Text = "Cambiar administrador:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        var adminPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(3, 6, 3, 6) };
        _adminYesRadio = new RadioButton { Text = "Sí", AutoSize = true, Enabled = false };
        _adminNoRadio = new RadioButton { Text = "No", AutoSize = true, Enabled = false, Checked = true, Margin = new Padding(12, 0, 3, 0) };
        _changeAdminCheck.CheckedChanged += (_, _) =>
        {
            _adminYesRadio.Enabled = _changeAdminCheck.Checked;
            _adminNoRadio.Enabled = _changeAdminCheck.Checked;
        };
        adminPanel.Controls.Add(_adminYesRadio);
        adminPanel.Controls.Add(_adminNoRadio);
        layout.Controls.Add(_changeAdminCheck, 0, row);
        layout.Controls.Add(adminPanel, 1, row);
        row++;

        _changeOrderCheck = new CheckBox { Text = "Cambiar orden a:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        _orderUpDown = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 80, Enabled = false, Margin = new Padding(3, 6, 3, 6) };
        _changeOrderCheck.CheckedChanged += (_, _) => _orderUpDown.Enabled = _changeOrderCheck.Checked;
        layout.Controls.Add(_changeOrderCheck, 0, row);
        layout.Controls.Add(_orderUpDown, 1, row);
        row++;

        _addTagsCheck = new CheckBox { Text = "Añadir tags:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        _tagsBox = new TextBox { Dock = DockStyle.Fill, Enabled = false, PlaceholderText = "separados por comas", Margin = new Padding(3, 6, 3, 6) };
        _addTagsCheck.CheckedChanged += (_, _) => _tagsBox.Enabled = _addTagsCheck.Checked;
        layout.Controls.Add(_addTagsCheck, 0, row);
        layout.Controls.Add(_tagsBox, 1, row);
        row++;

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(3, 16, 3, 3) };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        var okButton = AppTheme.CreateButton("Aplicar", primary: true);
        okButton.Click += OnApply;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, row);
        layout.SetColumnSpan(buttonsPanel, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        AppTheme.StyleForm(this);
    }

    private void OnBrowseInstallDir(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta destino de instalación" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installDirBox.Text = dialog.SelectedPath;
        }
    }

    private void OnApply(object? sender, EventArgs e)
    {
        if (!_changeArgumentsCheck.Checked && !_changeInstallDirCheck.Checked && !_changeAdminCheck.Checked &&
            !_changeOrderCheck.Checked && !_addTagsCheck.Checked)
        {
            MessageBox.Show(this, "Marca al menos un campo para aplicar un cambio.", "Nada que aplicar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_changeInstallDirCheck.Checked && string.IsNullOrWhiteSpace(_installDirBox.Text))
        {
            MessageBox.Show(this, "Indica una carpeta destino.", "Falta la carpeta",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var options = new BulkEditOptions
        {
            Arguments = _changeArgumentsCheck.Checked ? _argumentsBox.Text.Trim() : null,
            InstallDir = _changeInstallDirCheck.Checked ? _installDirBox.Text.Trim() : null,
            RunAsAdmin = _changeAdminCheck.Checked ? _adminYesRadio.Checked : null,
            Order = _changeOrderCheck.Checked ? (int)_orderUpDown.Value : null,
            AddTagsText = _addTagsCheck.Checked ? _tagsBox.Text : null,
        };
        BulkEditApplier.Apply(_entries, options);

        DialogResult = DialogResult.OK;
        Close();
    }
}
