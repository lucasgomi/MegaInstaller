using System.ComponentModel;
using MegaInstaller.App.Theming;
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
    private readonly CheckBox _elevateCheck;
    private readonly TextBox _searchBox;
    private readonly ToolTip _toolTip = new();

    public bool ManifestChanged { get; private set; }

    public InstallerLibraryForm(string folder)
    {
        _folder = folder;

        Text = $"Editor de programas - {folder}";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 640);

        _manifest = LoadManifest();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        // Button rows have to fit the button's whole footprint (height plus
        // its top/bottom margins) or the FlowLayoutPanel clips it at the bottom.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8, 4, 8, 4) };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.Controls.Add(new Label { Text = "Buscar:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 0);
        _searchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Nombre, archivo o tag..." };
        _searchBox.TextChanged += (_, _) => RefreshGrid();
        searchPanel.Controls.Add(_searchBox, 1, 0);
        root.Controls.Add(searchPanel, 0, 0);

        var actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };

        var addMenu = new ContextMenuStrip();
        addMenu.Items.Add(AppTheme.CreateMenuItem("Añadir archivo(s)...", OnAddFile));
        addMenu.Items.Add(AppTheme.CreateMenuItem("Añadir desde URL...", OnAddFromUrl));
        addMenu.Items.Add(AppTheme.CreateMenuItem("Añadir instalador web...", OnAddWebInstaller));
        addMenu.Items.Add(AppTheme.CreateMenuItem("Importar de la carpeta", OnImportFound));
        actionsPanel.Controls.Add(AppTheme.CreateDropdownButton("Añadir", addMenu, primary: true));

        var detectButton = MakeButton("Detectar tipo", OnDetectType);
        var editButton = MakeButton("Editar...", OnEdit);
        var bulkEditButton = MakeButton("Editar marcados...", OnBulkEdit);
        _toolTip.SetToolTip(bulkEditButton, "Edita a la vez los programas con la casilla marcada en la columna izquierda de la tabla.");
        var removeButton = MakeButton("Quitar", OnRemove);
        actionsPanel.Controls.AddRange(new Control[] { detectButton, editButton, bulkEditButton, removeButton });
        root.Controls.Add(actionsPanel, 0, 1);

        _grid = BuildGrid();
        root.Controls.Add(_grid, 0, 2);

        var installPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 6) };
        var installSelectedButton = MakeButton("Instalar seleccionados", OnInstallSelected, primary: true);
        var installAllButton = MakeButton("Instalar todo", OnInstallAll, primary: true);
        _stopOnErrorCheck = new CheckBox { Text = "Detener si falla uno", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(16, 8, 0, 0) };
        _elevateCheck = new CheckBox
        {
            Text = ElevationProbe.IsProcessElevated() ? "Ya se está ejecutando como administrador" : "Elevar permisos (un solo UAC)",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(16, 8, 0, 0),
            Enabled = !ElevationProbe.IsProcessElevated(),
        };
        installPanel.Controls.AddRange(new Control[] { installSelectedButton, installAllButton, _stopOnErrorCheck, _elevateCheck });
        root.Controls.Add(installPanel, 0, 3);

        RefreshGrid();
        AppTheme.StyleForm(this);
    }

    private static Button MakeButton(string text, EventHandler handler, bool primary = false)
    {
        var button = AppTheme.CreateButton(text, primary);
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
        GridStyle.Apply(grid);

        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Enabled", HeaderText = "", Width = 30 });
        var iconColumn = new DataGridViewImageColumn { DataPropertyName = "Icon", HeaderText = "", Width = 32, ImageLayout = DataGridViewImageCellLayout.Zoom };
        GridStyle.ApplyIconColumn(iconColumn);
        grid.Columns.Add(iconColumn);
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Nombre", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FileName", HeaderText = "Archivo", Width = 180, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "Tipo", Width = 100, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Arguments", HeaderText = "Argumentos", Width = 180, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Tags", HeaderText = "Tags", Width = 120, ReadOnly = true });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "RunAsAdmin", HeaderText = "Admin", Width = 55, ReadOnly = true,
            ToolTipText = "Pide elevación (UAC) al instalar. Como máximo se eleva un instalador a la vez.",
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Order", HeaderText = "Orden", Width = 55, ReadOnly = true,
            ToolTipText = "Los instaladores con el mismo Orden se instalan en paralelo; un Orden distinto espera a que termine el anterior.",
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Estado", Width = 120, ReadOnly = true });
        grid.RowTemplate.Height = 30;

        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEdit(this, EventArgs.Empty); };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) SaveManifest(); };

        var rowMenu = new ContextMenuStrip();
        rowMenu.Items.Add(AppTheme.CreateMenuItem("Editar...", OnEdit));
        rowMenu.Items.Add(AppTheme.CreateMenuItem("Renombrar...", OnRenameSelected));
        rowMenu.Items.Add(AppTheme.CreateMenuItem("Quitar", OnRemove));
        grid.ContextMenuStrip = rowMenu;
        // A right-click outside the current (possibly multi-row) selection
        // selects just that row first, matching Explorer; a right-click
        // inside an existing selection leaves it alone so "Quitar" can still
        // act on the whole selection.
        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || grid.Rows[e.RowIndex].Selected) return;
            grid.ClearSelection();
            grid.Rows[e.RowIndex].Selected = true;
        };

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
        var query = _manifest.Items.OrderBy(i => i.Order).AsEnumerable();

        var search = _searchBox.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i =>
                i.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                TagUtils.MatchesAny(i.Tags, search));
        }

        var rows = new BindingList<InstallerRow>(query.Select(i => new InstallerRow(i, _folder)).ToList());
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

        using var editForm = new EditInstallerForm(entry, _manifest.Instances, _folder);
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

        using var editForm = new EditInstallerForm(entry, _manifest.Instances, _folder);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
        SaveManifest();
        RefreshGrid();
    }

    private void OnAddWebInstaller(object? sender, EventArgs e)
    {
        using var dialog = new AddWebInstallerForm();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (_manifest.Items.Any(i => string.Equals(i.FileName, dialog.FileName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(i.MirrorUrl)))
        {
            MessageBox.Show(this, $"Ya hay un instalador web con el nombre de archivo \"{dialog.FileName}\".",
                "Ya existe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var entry = new InstallerEntry
        {
            Name = dialog.EntryName,
            FileName = dialog.FileName,
            MirrorUrl = dialog.MirrorUrl,
            ExpectedSha256 = dialog.PinnedSha256,
            Type = dialog.SelectedType,
            Arguments = SilentArgsCatalog.GetSuggestedArguments(dialog.SelectedType),
            Order = (_manifest.Items.Count + 1) * 10,
        };

        using var editForm = new EditInstallerForm(entry, _manifest.Instances, _folder);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
        ApplyExtractedIconIfUseful(dialog.ExtractedIcon, entry.Id);
        SaveManifest();
        RefreshGrid();
    }

    /// <summary>
    /// Saves the icon auto-extracted from a web installer's real bytes (see
    /// AddWebInstallerForm.ExtractedIcon) under CustomTheme and applies it as
    /// the card icon of whichever single instance this entry ended up
    /// belonging to - only when membership is unambiguous (exactly one
    /// instance, the common "this web app IS this instance" case) and that
    /// instance has no icon of its own yet, so a deliberate choice is never
    /// silently overridden.
    /// </summary>
    private void ApplyExtractedIconIfUseful(Image? icon, string entryId)
    {
        if (icon is null) return;

        var memberInstances = _manifest.Instances.Where(i => i.InstallerIds.Contains(entryId)).ToList();
        if (memberInstances.Count != 1 || !string.IsNullOrWhiteSpace(memberInstances[0].IconKey))
        {
            return;
        }

        try
        {
            var directory = Path.Combine(_folder, InstanceIconCatalog.CustomThemeFolderName);
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid():N}.png";
            icon.Save(Path.Combine(directory, fileName), System.Drawing.Imaging.ImageFormat.Png);
            memberInstances[0].IconKey = InstanceIconCatalog.CustomKey(fileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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

        using var editForm = new EditInstallerForm(row.Entry, _manifest.Instances, _folder);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            row.RefreshAll();
            SaveManifest();
        }
    }

    private void OnRenameSelected(object? sender, EventArgs e)
    {
        var rows = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as InstallerRow)
            .Where(r => r is not null)
            .Cast<InstallerRow>()
            .ToList();

        if (rows.Count != 1)
        {
            MessageBox.Show(this, "Selecciona un único programa para renombrar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var renameForm = new RenameForm("Renombrar programa", rows[0].Entry.Name);
        if (renameForm.ShowDialog(this) != DialogResult.OK) return;

        rows[0].Entry.Name = renameForm.NewName;
        rows[0].RefreshAll();
        SaveManifest();
    }

    private void OnBulkEdit(object? sender, EventArgs e)
    {
        var rows = GridRows.Where(r => r.Enabled).ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this,
                "Marca la casilla de los programas que quieres editar juntos (columna izquierda de la tabla).",
                "Nada marcado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var bulkForm = new BulkEditInstallersForm(rows.Select(r => r.Entry).ToList());
        if (bulkForm.ShowDialog(this) != DialogResult.OK) return;

        foreach (var row in rows)
        {
            row.RefreshAll();
        }

        SaveManifest();
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

        // One UAC prompt for the whole batch when asked for; if the prompt is
        // dismissed, TryLaunch returns false and it installs here instead.
        if (_elevateCheck.Checked && _elevateCheck.Enabled &&
            ElevatedInstallLauncher.TryLaunch(this, _folder, entries, _stopOnErrorCheck.Checked))
        {
            return;
        }

        using var progressForm = new InstallProgressForm(_folder, entries, _stopOnErrorCheck.Checked);
        progressForm.ShowDialog(this);
    }
}
