using System.ComponentModel;
using MegaInstaller.App.Dialogs;
using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App;

/// <summary>
/// Home screen: instances ("packs") come first, since that's what
/// MegaInstaller is built around. The full installer library (add/edit/
/// remove individual programs) lives one click away, in
/// <see cref="InstallerLibraryForm"/>.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ManifestService _manifestService = new();
    private readonly AppSettingsService _settingsService = new(AppSettingsService.DefaultPath);

    private InstallerManifest _manifest = new();
    private string _folder = string.Empty;

    private readonly DataGridView _grid;
    private readonly ToolTip _toolTip = new();

    public MainForm()
    {
        Text = "MegaInstaller";
        Width = 900;
        Height = 620;
        MinimumSize = new Size(700, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = LoadAppIcon();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var headerPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(10, 6, 10, 6), BackColor = Color.FromArgb(240, 243, 247) };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var appIcon = InstanceIconCatalog.Load("box-seam-fill");
        if (appIcon is not null)
        {
            headerPanel.Controls.Add(new PictureBox { Image = appIcon, Width = 28, Height = 28, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 4, 8, 0) }, 0, 0);
        }
        headerPanel.Controls.Add(new Label
        {
            Text = "MegaInstaller",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 0),
        }, 1, 0);
        var settingsButton = MakeButton("⚙ Ajustes", OnOpenSettings);
        settingsButton.Anchor = AnchorStyles.Right;
        _toolTip.SetToolTip(settingsButton, "Cambiar la carpeta de instaladores");
        headerPanel.Controls.Add(settingsButton, 2, 0);
        root.Controls.Add(headerPanel, 0, 0);

        var actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2) };
        actionsPanel.Controls.Add(MakeButton("Nueva instancia...", OnNewInstance));
        actionsPanel.Controls.Add(MakeButton("Editar instancia...", OnEditInstance));
        actionsPanel.Controls.Add(MakeButton("Eliminar instancia", OnRemoveInstance));
        actionsPanel.Controls.Add(MakeButton("Instalar instancia...", OnInstallInstance));
        var libraryButton = MakeButton("Editor de programas...", OnOpenLibrary);
        libraryButton.Margin = new Padding(40, 4, 4, 4);
        actionsPanel.Controls.Add(libraryButton);
        root.Controls.Add(actionsPanel, 0, 1);

        _grid = BuildGrid();
        root.Controls.Add(_grid, 0, 2);

        Load += (_, _) => EnsureFolderForThisSession();
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
        button.Click += handler;
        return button;
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
        };
        GridStyle.Apply(grid);

        var iconColumn = new DataGridViewImageColumn { DataPropertyName = "Icon", HeaderText = "", Width = 36, ImageLayout = DataGridViewImageCellLayout.Zoom };
        GridStyle.ApplyIconColumn(iconColumn);
        grid.Columns.Add(iconColumn);
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Instancia", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "Descripción", Width = 320 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProgramCount", HeaderText = "Programas", Width = 90, ReadOnly = true });
        grid.RowTemplate.Height = 32;

        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEditInstance(this, EventArgs.Empty); };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) SaveManifest(); };

        return grid;
    }

    /// <summary>
    /// Shows the folder picker once per Windows logon session (tracked via
    /// Process.SessionId) rather than on every single launch. Choosing
    /// "Salir" there closes the whole app instead of leaving Home half set up.
    /// </summary>
    private void EnsureFolderForThisSession()
    {
        var settings = _settingsService.Load();
        var currentSessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;

        if (settings.LastWindowsSessionId != currentSessionId)
        {
            using var startupForm = new SelectFolderStartupForm(settings.LastFolder);
            if (startupForm.ShowDialog(this) != DialogResult.OK || startupForm.SelectedFolder is null)
            {
                Application.Exit();
                return;
            }

            settings.LastFolder = startupForm.SelectedFolder;
            settings.LastWindowsSessionId = currentSessionId;
            _settingsService.Save(settings);
        }

        if (!string.IsNullOrWhiteSpace(settings.LastFolder) && Directory.Exists(settings.LastFolder))
        {
            LoadFolder(settings.LastFolder);
        }
    }

    private void LoadFolder(string folder)
    {
        try
        {
            _manifest = _manifestService.Load(folder);
        }
        catch (ManifestException ex)
        {
            var choice = MessageBox.Show(this,
                $"{ex.Message}\n\n¿Quieres hacer una copia de seguridad del archivo dañado y empezar de cero?",
                "No se pudo leer megainstaller.json", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            var manifestPath = _manifestService.GetManifestPath(folder);
            File.Copy(manifestPath, manifestPath + $".bak-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            _manifest = new InstallerManifest();
        }

        _folder = folder;

        var settings = _settingsService.Load();
        settings.LastFolder = folder;
        _settingsService.Save(settings);

        RefreshGrid();
    }

    private void RefreshGrid()
    {
        var rows = new BindingList<InstanceRow>(
            _manifest.Instances
                .OrderBy(i => i.Order)
                .Select(i => new InstanceRow(i, InstanceService.ResolveInstallers(_manifest, i).Count))
                .ToList());
        _grid.DataSource = rows;
    }

    private IEnumerable<InstanceRow> GridRows => ((BindingList<InstanceRow>?)_grid.DataSource) ?? Enumerable.Empty<InstanceRow>();

    private void SaveManifest()
    {
        if (string.IsNullOrEmpty(_folder)) return;
        _manifestService.Save(_folder, _manifest);
    }

    private bool EnsureFolderSelected()
    {
        if (!string.IsNullOrEmpty(_folder) && Directory.Exists(_folder)) return true;
        MessageBox.Show(this, "Primero selecciona una carpeta de instaladores.", "Falta la carpeta",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private InstanceRow? SelectedRow() =>
        _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as InstanceRow).FirstOrDefault();

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_folder);
        settingsForm.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(settingsForm.SelectedFolder) &&
            !string.Equals(settingsForm.SelectedFolder, _folder, StringComparison.OrdinalIgnoreCase))
        {
            LoadFolder(settingsForm.SelectedFolder);
        }
    }

    private void OnOpenLibrary(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        using var libraryForm = new InstallerLibraryForm(_folder);
        libraryForm.ShowDialog(this);

        // The library form works on its own copy of the manifest and saves
        // to disk as it goes; reload ours so instance program counts (and
        // any instance membership edited from there) reflect its changes.
        LoadFolder(_folder);
    }

    private void OnNewInstance(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        var instance = new InstanceDefinition { Order = (_manifest.Instances.Count + 1) * 10 };
        using var editForm = new EditInstanceForm(instance, _manifest.Items);
        if (editForm.ShowDialog(this) != DialogResult.OK) return;

        _manifest.Instances.Add(instance);
        SaveManifest();
        RefreshGrid();
    }

    private void OnEditInstance(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null)
        {
            MessageBox.Show(this, "Selecciona una instancia para editar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editForm = new EditInstanceForm(row.Instance, _manifest.Items);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            SaveManifest();
            RefreshGrid();
        }
    }

    private void OnRemoveInstance(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null)
        {
            MessageBox.Show(this, "Selecciona una instancia para eliminar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var choice = MessageBox.Show(this,
            $"¿Eliminar la instancia \"{row.Instance.Name}\"? Los instaladores que contiene no se borrarán.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return;

        _manifest.Instances.Remove(row.Instance);
        SaveManifest();
        RefreshGrid();
    }

    private void OnInstallInstance(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        var row = SelectedRow();
        if (row is null)
        {
            MessageBox.Show(this, "Selecciona una instancia para instalar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var resolved = InstanceService.ResolveInstallers(_manifest, row.Instance);
        if (resolved.Count == 0)
        {
            MessageBox.Show(this,
                "Esta instancia no tiene programas asociados todavía. Edítala para añadir alguno.",
                "Instancia vacía", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var installForm = new InstallInstanceForm(_folder, row.Instance, resolved);
        installForm.ShowDialog(this);
    }
}
