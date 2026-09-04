using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Installs several instances together in one batch/progress window instead
/// of one at a time. Always uses each instance's full, unmodified installer
/// list - per-run excludes or a custom install path stay in
/// <see cref="InstallInstanceForm"/>'s advanced mode for a single instance -
/// and an installer shared by more than one checked instance is only queued
/// once. Whether any of that queue actually needs admin, and for which
/// entries, is unchanged: <see cref="InstallService"/> still elevates only
/// the ones with <see cref="InstallerEntry.RunAsAdmin"/> set.
/// </summary>
public sealed class MultiInstallInstancesForm : Form
{
    private readonly string _folder;
    private readonly InstallerManifest _manifest;
    private readonly List<InstanceDefinition> _instances;
    private readonly CheckedListBox _instanceList;
    private readonly CheckBox _stopOnErrorCheck;
    private readonly CheckBox _elevateCheck;
    private readonly Button _installButton;

    public MultiInstallInstancesForm(string folder, InstallerManifest manifest)
    {
        _folder = folder;
        _manifest = manifest;
        _instances = manifest.Instances.OrderBy(i => i.Order).ToList();

        Text = "Instalar varias instancias";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 480);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Marca las instancias que quieres instalar juntas. Los programas que compartan varias solo se instalarán una vez.",
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);

        _instanceList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
        foreach (var instance in _instances)
        {
            var count = InstanceService.ResolveInstallers(_manifest, instance).Count;
            var countLabel = count == 1 ? "1 programa" : $"{count} programas";
            _instanceList.Items.Add($"{instance.Name} ({countLabel})");
        }
        if (_instances.Count == 0)
        {
            _instanceList.Items.Add("(No hay instancias todavía)");
            _instanceList.Enabled = false;
        }
        root.Controls.Add(_instanceList, 0, 1);

        var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        _stopOnErrorCheck = new CheckBox { Text = "Detener si falla uno", AutoSize = true };
        _elevateCheck = new CheckBox
        {
            Text = ElevationProbe.IsProcessElevated() ? "Ya se está ejecutando como administrador" : "Elevar permisos (un solo UAC)",
            AutoSize = true,
            Margin = new Padding(20, 3, 3, 3),
            Enabled = !ElevationProbe.IsProcessElevated(),
        };
        optionsPanel.Controls.Add(_stopOnErrorCheck);
        optionsPanel.Controls.Add(_elevateCheck);
        root.Controls.Add(optionsPanel, 0, 2);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        _installButton = AppTheme.CreateButton("Instalar", primary: true);
        _installButton.Enabled = _instances.Count > 0;
        _installButton.Click += OnInstallClick;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(_installButton);
        root.Controls.Add(buttonsPanel, 0, 3);

        CancelButton = cancelButton;
        AppTheme.StyleForm(this);
    }

    private IEnumerable<InstanceDefinition> CheckedInstances() =>
        _instances.Where((_, index) => _instanceList.GetItemChecked(index));

    private void OnInstallClick(object? sender, EventArgs e)
    {
        var checkedInstances = CheckedInstances().ToList();
        if (checkedInstances.Count == 0)
        {
            MessageBox.Show(this, "Marca al menos una instancia para instalar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var aggregated = new List<InstallerEntry>();
        var seenIds = new HashSet<string>();
        foreach (var instance in checkedInstances)
        {
            foreach (var entry in InstanceService.ResolveInstallers(_manifest, instance))
            {
                if (seenIds.Add(entry.Id))
                {
                    aggregated.Add(entry);
                }
            }
        }

        var plan = InstanceInstallPlanner.BuildPlan(aggregated);
        if (plan.Count == 0)
        {
            MessageBox.Show(this, "Ninguna de las instancias marcadas tiene programas asociados.", "Nada que instalar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // One UAC prompt for the whole batch when asked for; if it's
        // dismissed, TryLaunch returns false and it installs here instead.
        if (_elevateCheck.Checked && _elevateCheck.Enabled &&
            ElevatedInstallLauncher.TryLaunch(this, _folder, plan, _stopOnErrorCheck.Checked))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        using var progressForm = new InstallProgressForm(_folder, plan, _stopOnErrorCheck.Checked);
        progressForm.ShowDialog(this);

        DialogResult = DialogResult.OK;
        Close();
    }
}
