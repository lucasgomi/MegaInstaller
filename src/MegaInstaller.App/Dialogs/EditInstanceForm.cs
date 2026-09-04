using MegaInstaller.App.Theming;
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
    private readonly FlowLayoutPanel _iconPicker;
    private readonly FlowLayoutPanel _colorPicker;
    private readonly ToolTip _toolTip = new();
    private string? _selectedIconKey;
    private string? _selectedColorHex;

    private static Color IconSelectedColor => AppTheme.IsModern ? ModernPalette.AccentSoft : Color.FromArgb(204, 228, 247);
    private static Color IconUnselectedColor => AppTheme.IsModern ? ModernPalette.Surface : SystemColors.Control;

    public EditInstanceForm(InstanceDefinition instance, IReadOnlyList<InstallerEntry> allInstallers)
    {
        _instance = instance;
        _allInstallers = allInstallers;
        _selectedIconKey = instance.IconKey;
        _selectedColorHex = instance.ColorHex;

        Text = string.IsNullOrWhiteSpace(instance.Name) ? "Nueva instancia" : $"Editar instancia - {instance.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 666);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // RowStyles is a plain positional list - RowStyles[i] governs row i
        // regardless of when in the code below it's added, so every row's
        // style must be declared here, upfront, in row order. Adding a
        // style only after placing that row's controls silently applies it
        // to an earlier, still-unstyled row instead (which is what made
        // Nombre/Descripción render with the sizes meant for Icono/Programas).
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _nameBox = new TextBox { Dock = DockStyle.Fill, Text = instance.Name };
        layout.Controls.Add(_nameBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Descripción:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _descriptionBox = new TextBox { Dock = DockStyle.Fill, Text = instance.Description };
        layout.Controls.Add(_descriptionBox, 1, 1);

        layout.Controls.Add(new Label { Text = "Icono:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 2);
        _iconPicker = BuildIconPicker();
        layout.Controls.Add(_iconPicker, 1, 2);

        layout.Controls.Add(new Label { Text = "Color:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 3);
        _colorPicker = BuildColorPicker();
        layout.Controls.Add(_colorPicker, 1, 3);

        layout.Controls.Add(new Label { Text = "Programas:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 4);
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
        layout.Controls.Add(_installersList, 1, 4);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        var okButton = AppTheme.CreateButton("Guardar", primary: true);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += OnSave;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonsPanel, 0, 5);
        layout.SetColumnSpan(buttonsPanel, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        AppTheme.StyleForm(this);
    }

    private FlowLayoutPanel BuildIconPicker()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BorderStyle = BorderStyle.FixedSingle };

        var noneButton = MakeIconButton(null, "Sin icono");
        panel.Controls.Add(noneButton);

        foreach (var (key, displayName) in InstanceIconCatalog.Icons)
        {
            panel.Controls.Add(MakeIconButton(key, displayName));
        }

        HighlightSelectedIcon(panel);
        return panel;
    }

    private PictureBox MakeIconButton(string? key, string displayName)
    {
        var box = new PictureBox
        {
            Width = 40,
            Height = 40,
            Margin = new Padding(3),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Image = key is null ? null : InstanceIconCatalog.Load(key),
            Tag = key,
            Cursor = Cursors.Hand,
        };
        _toolTip.SetToolTip(box, displayName);
        box.Click += (_, _) =>
        {
            _selectedIconKey = key;
            HighlightSelectedIcon(_iconPicker);
        };
        return box;
    }

    private void HighlightSelectedIcon(FlowLayoutPanel panel)
    {
        foreach (PictureBox box in panel.Controls)
        {
            var key = box.Tag as string;
            box.BackColor = key == _selectedIconKey ? IconSelectedColor : IconUnselectedColor;
        }
    }

    private FlowLayoutPanel BuildColorPicker()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BorderStyle = BorderStyle.FixedSingle };

        panel.Controls.Add(MakeColorSwatch(null, "Por defecto (color del tema)"));
        foreach (var hex in InstanceColorPalette.Colors)
        {
            panel.Controls.Add(MakeColorSwatch(hex, hex));
        }

        return panel;
    }

    private Control MakeColorSwatch(string? hex, string displayName)
    {
        var color = hex is null ? (Color?)null : InstanceColorPalette.Resolve(hex, SystemColors.Control);
        var swatch = new Panel
        {
            Width = 34,
            Height = 34,
            Margin = new Padding(3),
            BackColor = color ?? SystemColors.ControlLightLight,
            Tag = hex,
            Cursor = Cursors.Hand,
        };
        swatch.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (color is null)
            {
                using var crossPen = new Pen(SystemColors.GrayText, 1.5f);
                e.Graphics.DrawLine(crossPen, 7, 7, swatch.Width - 8, swatch.Height - 8);
                e.Graphics.DrawLine(crossPen, swatch.Width - 8, 7, 7, swatch.Height - 8);
            }

            var isSelected = hex == _selectedColorHex;
            using var borderPen = new Pen(isSelected ? SystemColors.WindowText : Color.FromArgb(60, SystemColors.WindowText), isSelected ? 2.5f : 1f);
            e.Graphics.DrawRectangle(borderPen, 0, 0, swatch.Width - 1, swatch.Height - 1);
        };
        _toolTip.SetToolTip(swatch, displayName);
        swatch.Click += (_, _) =>
        {
            _selectedColorHex = hex;
            foreach (Control sibling in _colorPicker.Controls)
            {
                sibling.Invalidate();
            }
        };
        return swatch;
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
        _instance.IconKey = _selectedIconKey;
        _instance.ColorHex = _selectedColorHex;

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
