using MegaInstaller.App.Theming;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Quick rename for a context menu's "Renombrar..." entry - just a single
/// name field, with none of the full editor's other UI, for when that's
/// overkill for a one-word typo fix. Reused for both instance cards and
/// individual installer library rows.
/// </summary>
public sealed class RenameForm : Form
{
    private readonly TextBox _nameBox;

    public string NewName => _nameBox.Text.Trim();

    public RenameForm(string title, string currentName)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 128);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        _nameBox = new TextBox { Dock = DockStyle.Fill, Text = currentName };
        layout.Controls.Add(_nameBox, 0, 1);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        var okButton = AppTheme.CreateButton("Renombrar", primary: true);
        okButton.Click += OnRename;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        _nameBox.SelectAll();
        AppTheme.StyleForm(this);
    }

    private void OnRename(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "El nombre no puede estar vacío.", "Falta el nombre",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
