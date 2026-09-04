using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Lets the user install an instance either the easy way (everything,
/// automatic paths) or the advanced way (exclude specific installers for
/// this run, optionally redirect supported ones to a custom folder) before
/// handing the resulting plan to <see cref="InstallProgressForm"/>.
/// </summary>
public sealed class InstallInstanceForm : Form
{
    private readonly string _folder;
    private readonly List<InstallerEntry> _resolvedEntries;

    private readonly RadioButton _easyModeRadio;
    private readonly RadioButton _advancedModeRadio;
    private readonly Panel _easyPanel;
    private readonly Panel _advancedPanel;
    private readonly CheckedListBox _includeList;
    private readonly TextBox _overrideDirBox;
    private readonly CheckBox _stopOnErrorCheck;
    private readonly Button _installButton;

    public InstallInstanceForm(string folder, InstanceDefinition instance, List<InstallerEntry> resolvedEntries)
    {
        _folder = folder;
        _resolvedEntries = resolvedEntries;

        Text = $"Instalar - {instance.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 500);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var headerText = string.IsNullOrWhiteSpace(instance.Description)
            ? $"{resolvedEntries.Count} programa(s) en esta instancia."
            : $"{instance.Description}\n{resolvedEntries.Count} programa(s) en esta instancia.";
        var headerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var icon = InstanceIconCatalog.LoadForInstance(instance.IconKey, folder);
        if (icon is not null)
        {
            headerPanel.Controls.Add(new PictureBox
            {
                Image = icon,
                Width = 36,
                Height = 36,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 10, 0),
            });
        }
        headerPanel.Controls.Add(new Label { Text = headerText, AutoSize = true, MaximumSize = new Size(440, 0), Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) });
        root.Controls.Add(headerPanel, 0, 0);

        var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _easyModeRadio = new RadioButton { Text = "Modo fácil (todo, rutas automáticas)", AutoSize = true, Checked = true };
        _advancedModeRadio = new RadioButton { Text = "Modo avanzado", AutoSize = true, Margin = new Padding(20, 0, 0, 0) };
        _easyModeRadio.CheckedChanged += (_, _) => UpdateModePanels();
        modePanel.Controls.Add(_easyModeRadio);
        modePanel.Controls.Add(_advancedModeRadio);
        root.Controls.Add(modePanel, 0, 1);

        _easyPanel = BuildEasyPanel();
        _advancedPanel = BuildAdvancedPanel(out _includeList, out _overrideDirBox);
        var contentHost = new Panel { Dock = DockStyle.Fill };
        contentHost.Controls.Add(_easyPanel);
        contentHost.Controls.Add(_advancedPanel);
        root.Controls.Add(contentHost, 0, 2);

        _stopOnErrorCheck = new CheckBox { Text = "Detener si falla uno", AutoSize = true };
        root.Controls.Add(_stopOnErrorCheck, 0, 3);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        _installButton = AppTheme.CreateButton("Instalar", primary: true);
        _installButton.Enabled = resolvedEntries.Count > 0;
        _installButton.Click += OnInstallClick;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(_installButton);
        root.Controls.Add(buttonsPanel, 0, 4);

        CancelButton = cancelButton;
        UpdateModePanels();
        AppTheme.StyleForm(this);
    }

    private Panel BuildEasyPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Items.AddRange(_resolvedEntries.Select(e => (object)$"{e.Name} ({e.FileName})").ToArray());
        if (_resolvedEntries.Count == 0)
        {
            list.Items.Add("(Esta instancia no tiene programas asociados todavía)");
        }
        panel.Controls.Add(list);
        return panel;
    }

    private Panel BuildAdvancedPanel(out CheckedListBox includeList, out TextBox overrideDirBox)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(layout);

        includeList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        foreach (var entry in _resolvedEntries)
        {
            includeList.Items.Add($"{entry.Name} ({entry.FileName})", isChecked: true);
        }
        layout.Controls.Add(includeList, 0, 0);

        var dirPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        overrideDirBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Carpeta de instalación personalizada (opcional)" };
        var browseButton = AppTheme.CreateButton("...");
        var overrideDirBoxRef = overrideDirBox;
        browseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta destino de instalación" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                overrideDirBoxRef.Text = dialog.SelectedPath;
            }
        };
        dirPanel.Controls.Add(overrideDirBox, 0, 0);
        dirPanel.Controls.Add(browseButton, 1, 0);
        layout.Controls.Add(dirPanel, 0, 1);

        layout.Controls.Add(new Label
        {
            Text = "La carpeta personalizada solo se aplica a instaladores MSI, Inno Setup y NSIS; el resto usará su configuración habitual.",
            AutoSize = true,
            MaximumSize = new Size(470, 0),
            ForeColor = SystemColors.GrayText,
        }, 0, 2);

        return panel;
    }

    private void UpdateModePanels()
    {
        _easyPanel.Visible = _easyModeRadio.Checked;
        _advancedPanel.Visible = !_easyModeRadio.Checked;
        _easyPanel.Dock = DockStyle.Fill;
        _advancedPanel.Dock = DockStyle.Fill;
    }

    private void OnInstallClick(object? sender, EventArgs e)
    {
        List<InstallerEntry> plan;

        if (_easyModeRadio.Checked)
        {
            plan = InstanceInstallPlanner.BuildPlan(_resolvedEntries);
        }
        else
        {
            var excluded = new HashSet<string>();
            for (var i = 0; i < _resolvedEntries.Count; i++)
            {
                if (!_includeList.GetItemChecked(i))
                {
                    excluded.Add(_resolvedEntries[i].Id);
                }
            }

            var overrideDir = string.IsNullOrWhiteSpace(_overrideDirBox.Text) ? null : _overrideDirBox.Text.Trim();
            plan = InstanceInstallPlanner.BuildPlan(_resolvedEntries, excluded, overrideDir);
        }

        if (plan.Count == 0)
        {
            MessageBox.Show(this, "No hay programas seleccionados para instalar.", "Nada que instalar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var progressForm = new InstallProgressForm(_folder, plan, _stopOnErrorCheck.Checked);
        progressForm.ShowDialog(this);

        DialogResult = DialogResult.OK;
        Close();
    }
}
