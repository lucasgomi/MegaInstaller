using MegaInstaller.Core.Models;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Create or edit an instance ("pack"): its name/description and which
/// installers (from the whole library) belong to it. Membership can also be
/// set from the other direction, in <see cref="EditInstallerForm"/>.
/// </summary>
public sealed class EditInstanceForm : Form
{
    private readonly InstanceDefinition _instance;
    private readonly IReadOnlyList<InstallerEntry> _allInstallers;

    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;
    private readonly CheckedListBox _installersList;

    public EditInstanceForm(InstanceDefinition instance, IReadOnlyList<InstallerEntry> allInstallers)
    {
        _instance = instance;
        _allInstallers = allInstallers;

        Text = string.IsNullOrWhiteSpace(instance.Name) ? "Nueva instancia" : $"Editar instancia - {instance.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 480);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _nameBox = new TextBox { Dock = DockStyle.Fill, Text = instance.Name };
        layout.Controls.Add(_nameBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Descripción:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _descriptionBox = new TextBox { Dock = DockStyle.Fill, Text = instance.Description };
        layout.Controls.Add(_descriptionBox, 1, 1);

        layout.Controls.Add(new Label { Text = "Programas:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 2);
        _installersList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        if (_allInstallers.Count == 0)
        {
            _installersList.Items.Add("(No hay instaladores en esta carpeta; añade alguno desde el Editor de programas)");
            _installersList.Enabled = false;
        }
        else
        {
            foreach (var installer in _allInstallers)
            {
                var isMember = instance.InstallerIds.Contains(installer.Id);
                _installersList.Items.Add($"{installer.Name} ({installer.FileName})", isMember);
            }
        }
        layout.Controls.Add(_installersList, 1, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        var okButton = new Button { Text = "Guardar", DialogResult = DialogResult.OK, AutoSize = true };
        okButton.Click += OnSave;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, 3);
        layout.SetColumnSpan(buttonsPanel, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;
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

        _instance.Name = _nameBox.Text.Trim();
        _instance.Description = _descriptionBox.Text.Trim();

        if (_allInstallers.Count > 0)
        {
            // _installersList items were added in the same order as _allInstallers, one per installer.
            var memberIds = new List<string>();
            for (var i = 0; i < _allInstallers.Count; i++)
            {
                if (_installersList.GetItemChecked(i))
                {
                    memberIds.Add(_allInstallers[i].Id);
                }
            }

            _instance.InstallerIds = memberIds;
        }
    }
}
