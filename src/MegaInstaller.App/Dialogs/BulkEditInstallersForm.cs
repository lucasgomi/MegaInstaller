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
        ClientSize = new Size(520, 400);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var row = 0;

        var headerLabel = new Label
        {
            Text = $"Editando {entries.Count} programa(s). Solo se aplican los campos marcados; el resto se deja como estaba.",
            AutoSize = true,
            MaximumSize = new Size(460, 0),
        };
        layout.Controls.Add(headerLabel, 0, row);
        layout.SetColumnSpan(headerLabel, 2);
        row++;

        _changeArgumentsCheck = new CheckBox { Text = "Cambiar argumentos a:", AutoSize = true, Anchor = AnchorStyles.Left };
        _argumentsBox = new TextBox { Dock = DockStyle.Fill, Enabled = false };
        _changeArgumentsCheck.CheckedChanged += (_, _) => _argumentsBox.Enabled = _changeArgumentsCheck.Checked;
        layout.Controls.Add(_changeArgumentsCheck, 0, row);
        layout.Controls.Add(_argumentsBox, 1, row);
        row++;

        _changeInstallDirCheck = new CheckBox { Text = "Añadir carpeta destino:", AutoSize = true, Anchor = AnchorStyles.Left };
        var installDirPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        installDirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        installDirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _installDirBox = new TextBox { Dock = DockStyle.Fill, Enabled = false };
        var browseButton = new Button { Text = "...", AutoSize = true, Enabled = false };
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

        layout.Controls.Add(new Label
        {
            Text = "Solo se aplica a los programas MSI, Inno Setup o NSIS de la selección.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        }, 1, row);
        row++;

        _changeAdminCheck = new CheckBox { Text = "Cambiar \"Ejecutar como administrador\":", AutoSize = true, Anchor = AnchorStyles.Left };
        var adminPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _adminYesRadio = new RadioButton { Text = "Sí", AutoSize = true, Enabled = false };
        _adminNoRadio = new RadioButton { Text = "No", AutoSize = true, Enabled = false, Checked = true };
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

        _changeOrderCheck = new CheckBox { Text = "Cambiar orden a:", AutoSize = true, Anchor = AnchorStyles.Left };
        _orderUpDown = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 80, Enabled = false };
        _changeOrderCheck.CheckedChanged += (_, _) => _orderUpDown.Enabled = _changeOrderCheck.Checked;
        layout.Controls.Add(_changeOrderCheck, 0, row);
        layout.Controls.Add(_orderUpDown, 1, row);
        row++;

        _addTagsCheck = new CheckBox { Text = "Añadir tags:", AutoSize = true, Anchor = AnchorStyles.Left };
        _tagsBox = new TextBox { Dock = DockStyle.Fill, Enabled = false, PlaceholderText = "separados por comas" };
        _addTagsCheck.CheckedChanged += (_, _) => _tagsBox.Enabled = _addTagsCheck.Checked;
        layout.Controls.Add(_addTagsCheck, 0, row);
        layout.Controls.Add(_tagsBox, 1, row);
        row++;

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        var okButton = new Button { Text = "Aplicar", AutoSize = true };
        okButton.Click += OnApply;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, row);
        layout.SetColumnSpan(buttonsPanel, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;
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
