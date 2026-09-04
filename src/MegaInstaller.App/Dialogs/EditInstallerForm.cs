using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Lets the user configure exactly how one installer runs: custom silent
/// flags, a target install directory, elevation, and install order. This is
/// the "everything needed from inside the app" piece - editing here writes
/// straight back into the entry that gets saved to megainstaller.json.
/// </summary>
public sealed class EditInstallerForm : Form
{
    private readonly InstallerEntry _entry;
    private readonly IReadOnlyList<InstanceDefinition> _instances;

    private readonly TextBox _nameBox;
    private readonly ComboBox _typeCombo;
    private readonly TextBox _argumentsBox;
    private readonly TextBox _installDirBox;
    private readonly CheckBox _runAsAdminCheck;
    private readonly NumericUpDown _orderUpDown;
    private readonly TextBox _notesBox;
    private readonly TextBox _tagsBox;
    private readonly CheckedListBox _instancesList;

    public EditInstallerForm(InstallerEntry entry, IReadOnlyList<InstanceDefinition> instances)
    {
        _entry = entry;
        _instances = instances;

        Text = $"Editar - {entry.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 500);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 11,
            AutoSize = false,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        // RowStyles is a plain positional list - RowStyles[i] governs row i
        // regardless of when in the code below it's added, so every row's
        // style must be declared here, upfront, in row order. Adding a
        // style only after placing that row's controls silently applies it
        // to an earlier, still-unstyled row instead.
        int[] rowHeights = { 28, 32, 32, 32, 32, 32, 32, 76, 32, 96, 40 };
        foreach (var height in rowHeights)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        Controls.Add(layout);

        var row = 0;

        layout.Controls.Add(new Label { Text = "Archivo:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        var fileLabel = new Label { Text = entry.FileName, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
        layout.Controls.Add(fileLabel, 1, row);
        layout.SetColumnSpan(fileLabel, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _nameBox = new TextBox { Dock = DockStyle.Fill, Text = entry.Name };
        layout.Controls.Add(_nameBox, 1, row);
        layout.SetColumnSpan(_nameBox, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Tipo:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _typeCombo.Items.AddRange(Enum.GetNames<InstallerType>());
        _typeCombo.SelectedItem = entry.Type.ToString();
        layout.Controls.Add(_typeCombo, 1, row);
        var suggestButton = AppTheme.CreateButton("Sugerir flags");
        suggestButton.AutoSize = false;
        suggestButton.Dock = DockStyle.Fill;
        suggestButton.Click += OnSuggestArguments;
        layout.Controls.Add(suggestButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "Argumentos:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _argumentsBox = new TextBox { Dock = DockStyle.Fill, Text = entry.Arguments };
        layout.Controls.Add(_argumentsBox, 1, row);
        layout.SetColumnSpan(_argumentsBox, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Carpeta destino:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _installDirBox = new TextBox { Dock = DockStyle.Fill, Text = entry.TargetInstallDir };
        layout.Controls.Add(_installDirBox, 1, row);
        var browseDirButton = AppTheme.CreateButton("...");
        browseDirButton.AutoSize = false;
        browseDirButton.Dock = DockStyle.Fill;
        browseDirButton.Click += OnBrowseInstallDir;
        layout.Controls.Add(browseDirButton, 2, row);
        row++;

        layout.Controls.Add(new Label(), 0, row);
        var insertDirButton = AppTheme.CreateButton("Insertar carpeta en argumentos");
        insertDirButton.Dock = DockStyle.Fill;
        insertDirButton.Click += OnInsertInstallDir;
        layout.Controls.Add(insertDirButton, 1, row);
        layout.SetColumnSpan(insertDirButton, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Orden:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _orderUpDown = new NumericUpDown { Minimum = 0, Maximum = 9999, Value = Math.Clamp(entry.Order, 0, 9999), Width = 80 };
        _runAsAdminCheck = new CheckBox { Text = "Ejecutar como administrador (pedirá UAC)", AutoSize = true, Checked = entry.RunAsAdmin, Anchor = AnchorStyles.Left };
        var orderAndAdminPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = true };
        orderAndAdminPanel.Controls.Add(_orderUpDown);
        orderAndAdminPanel.Controls.Add(_runAsAdminCheck);
        layout.Controls.Add(orderAndAdminPanel, 1, row);
        layout.SetColumnSpan(orderAndAdminPanel, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Notas:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _notesBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 70, Text = entry.Notes };
        layout.Controls.Add(_notesBox, 1, row);
        layout.SetColumnSpan(_notesBox, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Tags:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _tagsBox = new TextBox { Dock = DockStyle.Fill, Text = TagUtils.Join(entry.Tags), PlaceholderText = "separados por comas, p. ej.: dev, cli" };
        layout.Controls.Add(_tagsBox, 1, row);
        layout.SetColumnSpan(_tagsBox, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Instancias:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, row);
        _instancesList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, Height = 90 };
        if (_instances.Count == 0)
        {
            _instancesList.Items.Add("(No hay instancias creadas todavía)");
            _instancesList.Enabled = false;
        }
        else
        {
            foreach (var instance in _instances)
            {
                var isMember = instance.InstallerIds.Contains(entry.Id);
                _instancesList.Items.Add(instance.Name, isMember);
            }
        }
        layout.Controls.Add(_instancesList, 1, row);
        layout.SetColumnSpan(_instancesList, 2);
        row++;

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        var okButton = AppTheme.CreateButton("Guardar", primary: true);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += OnSave;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, row);
        layout.SetColumnSpan(buttonsPanel, 3);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        AppTheme.StyleForm(this);
    }

    private InstallerType SelectedType =>
        Enum.TryParse<InstallerType>(_typeCombo.SelectedItem as string, out var type) ? type : InstallerType.Unknown;

    private void OnSuggestArguments(object? sender, EventArgs e)
    {
        var suggestion = SilentArgsCatalog.GetSuggestedArguments(SelectedType);
        if (string.IsNullOrEmpty(suggestion))
        {
            MessageBox.Show(this,
                "No hay flags silenciosos conocidos para este tipo. Puedes escribirlos manualmente.",
                "Sin sugerencia", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _argumentsBox.Text = suggestion;
    }

    private void OnBrowseInstallDir(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta destino de instalación" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installDirBox.Text = dialog.SelectedPath;
        }
    }

    private void OnInsertInstallDir(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_installDirBox.Text))
        {
            MessageBox.Show(this, "Indica primero una carpeta destino.", "Falta la carpeta",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var type = SelectedType;
        if (type is InstallerType.Unknown or InstallerType.Custom or InstallerType.InstallShield)
        {
            MessageBox.Show(this,
                "Este tipo de instalador no tiene un flag de carpeta destino fiable y universal. " +
                "Añade el argumento correcto manualmente si lo conoces.",
                "No soportado automáticamente", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _argumentsBox.Text = SilentArgsCatalog.AppendInstallDir(_argumentsBox.Text, type, _installDirBox.Text.Trim());
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "El nombre no puede estar vacío.", "Falta el nombre",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _entry.Name = _nameBox.Text.Trim();
        _entry.Type = SelectedType;
        _entry.Arguments = _argumentsBox.Text.Trim();
        _entry.TargetInstallDir = string.IsNullOrWhiteSpace(_installDirBox.Text) ? null : _installDirBox.Text.Trim();
        _entry.RunAsAdmin = _runAsAdminCheck.Checked;
        _entry.Order = (int)_orderUpDown.Value;
        _entry.Notes = _notesBox.Text.Trim();
        _entry.Tags = TagUtils.Parse(_tagsBox.Text);

        if (_instances.Count > 0)
        {
            // _instancesList items were added in the same order as _instances, one per instance.
            var memberOfIds = new HashSet<string>();
            for (var i = 0; i < _instances.Count; i++)
            {
                if (_instancesList.GetItemChecked(i))
                {
                    memberOfIds.Add(_instances[i].Id);
                }
            }

            InstanceService.ApplyMembership(_instances, _entry.Id, memberOfIds);
        }
    }
}
