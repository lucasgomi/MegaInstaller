using System.ComponentModel;
using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// The installer "library" for one folder: add/edit/remove installers,
/// detect their type, and install them directly (selected or all). This is
/// the screen the Home (instances) window's "Editor de programas" button
/// opens; it owns no folder-picking UI of its own - the folder is fixed for
/// the lifetime of the dialog.
/// </summary>
public sealed class InstallerLibraryForm : Form
{
    private readonly ManifestService _manifestService = new();
    private readonly string _folder;
    private InstallerManifest _manifest;

    private readonly DataGridView _grid;
    private readonly CheckBox _stopOnErrorCheck;

    public bool ManifestChanged { get; private set; }

    public InstallerLibraryForm(string folder)
    {
        _folder = folder;

        Text = $"Editor de programas - {folder}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(980, 620);
        MinimumSize = new Size(760, 440);

        _manifest = LoadManifest();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        var actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
        var addFileButton = MakeButton("Añadir archivo(s)...", OnAddFile);
        var addUrlButton = MakeButton("Añadir desde URL...", OnAddFromUrl);
        var importButton = MakeButton("Importar de la carpeta", OnImportFound);
        var detectButton = MakeButton("Detectar tipo", OnDetectType);
        var editButton = MakeButton("Editar...", OnEdit);
        var removeButton = MakeButton("Quitar", OnRemove);
        actionsPanel.Controls.AddRange(new Control[] { addFileButton, addUrlButton, importButton, detectButton, editButton, removeButton });
        root.Controls.Add(actionsPanel, 0, 0);

        _grid = BuildGrid();
        root.Controls.Add(_grid, 0, 1);

        var installPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 6) };
        var installSelectedButton = MakeButton("Instalar seleccionados", OnInstallSelected);
        var installAllButton = MakeButton("Instalar todo", OnInstallAll);
        _stopOnErrorCheck = new CheckBox { Text = "Detener si falla uno", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(16, 8, 0, 0) };
        installPanel.Controls.AddRange(new Control[] { installSelectedButton, installAllButton, _stopOnErrorCheck });
        root.Controls.Add(installPanel, 0, 2);

        RefreshGrid();
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
            MultiSelect = true,
            RowHeadersVisible = false,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Enabled", HeaderText = "", Width = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Nombre", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FileName", HeaderText = "Archivo", Width = 180, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "Tipo", Width = 100, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Arguments", HeaderText = "Argumentos", Width = 180, ReadOnly = true });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "RunAsAdmin", HeaderText = "Admin", Width = 55, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Order", HeaderText = "Orden", Width = 55, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Estado", Width = 120, ReadOnly = true });

        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEdit(this, EventArgs.Empty); };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) SaveManifest(); };

        return grid;
    }

    private InstallerManifest LoadManifest()
    {
        try
        {
            return _manifestService.Load(_folder);
        }
        catch (ManifestException ex)
        {
            var choice = MessageBox.Show(this,
                $"{ex.Message}\n\n¿Quieres hacer una copia de seguridad del archivo dañado y empezar de cero?",
                "No se pudo leer megainstaller.json", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            if (choice == DialogResult.Yes)
            {
                var manifestPath = _manifestService.GetManifestPath(_folder);
                File.Copy(manifestPath, manifestPath + $".bak-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            }

            return new InstallerManifest();
        }
    }

    private void RefreshGrid()
    {
        var rows = new BindingList<InstallerRow>(
            _manifest.Items.OrderBy(i => i.Order).Select(i => new InstallerRow(i)).ToList());
        _grid.DataSource = rows;
    }

    private IEnumerable<InstallerRow> GridRows => ((BindingList<InstallerRow>?)_grid.DataSource) ?? Enumerable.Empty<InstallerRow>();

    private void SaveManifest()
    {
        _manifestService.Save(_folder, _manifest);
        ManifestChanged = true;
    }

    private void OnAddFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Selecciona instaladores para añadir",
            Filter = "Instaladores (*.exe;*.msi)|*.exe;*.msi|Todos los archivos (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        foreach (var sourcePath in dialog.FileNames)
        {
            AddInstallerFile(sourcePath);
        }

        SaveManifest();
        RefreshGrid();
    }

    private void AddInstallerFile(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destinationPath = Path.Combine(_folder, fileName);

        var alreadyInFolder = string.Equals(
            Path.GetFullPath(Path.GetDirectoryName(sourcePath) ?? ""),
            Path.GetFullPath(_folder),
            StringComparison.OrdinalIgnoreCase);

        if (!alreadyInFolder)
        {
            if (File.Exists(destinationPath))
            {
                var overwrite = MessageBox.Show(this,
                    $"Ya existe \"{fileName}\" en la carpeta. ¿Sobrescribir?",
                    "El archivo ya existe", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (overwrite != DialogResult.Yes) return;
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        if (_manifest.Items.Any(i => string.Equals(i.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var type = InstallerTypeDetector.Detect(destinationPath);
        var entry = new InstallerEntry
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            Type = type,
            Arguments = SilentArgsCatalog.GetSuggestedArguments(type),
            Order = (_manifest.Items.Count + 1) * 10,
        };

        using var editForm = new EditInstallerForm(entry, _manifest.Instances);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
    }

    private void OnAddFromUrl(object? sender, EventArgs e)
    {
        using var dialog = new AddFromUrlForm(_folder);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.DownloadedFileName is null) return;

        var fileName = dialog.DownloadedFileName;
        var fullPath = Path.Combine(_folder, fileName);
        var type = InstallerTypeDetector.Detect(fullPath);

        var entry = new InstallerEntry
        {
            Name = string.IsNullOrWhiteSpace(dialog.SuggestedName) ? Path.GetFileNameWithoutExtension(fileName) : dialog.SuggestedName,
            FileName = fileName,
            SourceUrl = dialog.Url,
            Type = type,
            Arguments = SilentArgsCatalog.GetSuggestedArguments(type),
            Order = (_manifest.Items.Count + 1) * 10,
        };

        using var editForm = new EditInstallerForm(entry, _manifest.Instances);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
        SaveManifest();
        RefreshGrid();
    }

    private void OnImportFound(object? sender, EventArgs e)
    {
        var untracked = FolderScanner.FindUntrackedInstallers(_folder, _manifest);
        if (untracked.Count == 0)
        {
            MessageBox.Show(this, "No se encontraron instaladores nuevos en la carpeta.", "Nada que importar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var choice = MessageBox.Show(this,
            $"Se encontraron {untracked.Count} archivo(s) sin registrar:\n\n{string.Join('\n', untracked)}\n\n¿Añadirlos con la configuración detectada automáticamente?",
            "Importar instaladores", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return;

        foreach (var fileName in untracked)
        {
            var fullPath = Path.Combine(_folder, fileName);
            var type = InstallerTypeDetector.Detect(fullPath);
            _manifest.Items.Add(new InstallerEntry
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                FileName = fileName,
                Type = type,
                Arguments = SilentArgsCatalog.GetSuggestedArguments(type),
                Order = (_manifest.Items.Count + 1) * 10,
            });
        }

        SaveManifest();
        RefreshGrid();
    }

    private void OnDetectType(object? sender, EventArgs e)
    {
        var rows = SelectedRowsOrAll();
        foreach (var row in rows)
        {
            var fullPath = Path.Combine(_folder, row.Entry.FileName);
            row.Entry.Type = InstallerTypeDetector.Detect(fullPath);
            if (string.IsNullOrWhiteSpace(row.Entry.Arguments))
            {
                row.Entry.Arguments = SilentArgsCatalog.GetSuggestedArguments(row.Entry.Type);
            }
            row.RefreshAll();
        }

        SaveManifest();
    }

    private void OnEdit(object? sender, EventArgs e)
    {
        var row = SelectedRowsOrAll().FirstOrDefault();
        if (row is null)
        {
            MessageBox.Show(this, "Selecciona un instalador para editar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editForm = new EditInstallerForm(row.Entry, _manifest.Instances);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            row.RefreshAll();
            SaveManifest();
        }
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        var rows = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as InstallerRow)
            .Where(r => r is not null)
            .Cast<InstallerRow>()
            .ToList();

        if (rows.Count == 0)
        {
            MessageBox.Show(this, "Selecciona uno o más instaladores para quitar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var choice = MessageBox.Show(this,
            $"¿Quitar {rows.Count} elemento(s) de la lista? El archivo no se borrará del disco. También se quitarán de cualquier instancia que los use.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return;

        foreach (var row in rows)
        {
            _manifest.Items.RemoveAll(i => i.Id == row.Entry.Id);
            foreach (var instance in _manifest.Instances)
            {
                instance.InstallerIds.Remove(row.Entry.Id);
            }
        }

        SaveManifest();
        RefreshGrid();
    }

    private List<InstallerRow> SelectedRowsOrAll()
    {
        var selected = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as InstallerRow)
            .Where(r => r is not null)
            .Cast<InstallerRow>()
            .ToList();

        return selected.Count > 0 ? selected : GridRows.ToList();
    }

    private void OnInstallSelected(object? sender, EventArgs e) =>
        RunInstall(SelectedRowsOrAll().Where(r => r.Enabled).Select(r => r.Entry).ToList());

    private void OnInstallAll(object? sender, EventArgs e) =>
        RunInstall(GridRows.Where(r => r.Enabled).Select(r => r.Entry).ToList());

    private void RunInstall(List<InstallerEntry> entries)
    {
        if (entries.Count == 0)
        {
            MessageBox.Show(this, "No hay instaladores habilitados para instalar.", "Nada que instalar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var progressForm = new InstallProgressForm(_folder, entries, _stopOnErrorCheck.Checked);
        progressForm.ShowDialog(this);
    }
}
