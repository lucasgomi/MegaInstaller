using System.ComponentModel;
using MegaInstaller.App.Dialogs;
using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App;

public sealed class MainForm : Form
{
    private readonly ManifestService _manifestService = new();
    private readonly AppSettingsService _settingsService = new(AppSettingsService.DefaultPath);
    private readonly InstallService _installService = new();

    private InstallerManifest _manifest = new();
    private string _folder = string.Empty;
    private CancellationTokenSource? _installCts;
    private bool _installing;

    private readonly TextBox _folderTextBox;
    private readonly DataGridView _grid;
    private readonly RichTextBox _logBox;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;
    private readonly CheckBox _stopOnErrorCheck;
    private readonly Button _installSelectedButton;
    private readonly Button _installAllButton;
    private readonly Button _cancelInstallButton;

    public MainForm()
    {
        Text = "MegaInstaller";
        Width = 1080;
        Height = 760;
        MinimumSize = new Size(880, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        // Row 0: folder picker
        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(8, 6, 8, 6) };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.Controls.Add(new Label { Text = "Carpeta de instaladores:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _folderTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(6, 4, 6, 4) };
        folderPanel.Controls.Add(_folderTextBox, 1, 0);
        var browseButton = new Button { Text = "Examinar...", AutoSize = true, Margin = new Padding(2, 2, 2, 2) };
        browseButton.Click += OnBrowseFolder;
        folderPanel.Controls.Add(browseButton, 2, 0);
        var openFolderButton = new Button { Text = "Abrir carpeta", AutoSize = true, Margin = new Padding(2, 2, 2, 2) };
        openFolderButton.Click += OnOpenFolder;
        folderPanel.Controls.Add(openFolderButton, 3, 0);
        root.Controls.Add(folderPanel, 0, 0);

        // Row 1: actions
        var actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2) };
        var addFileButton = MakeButton("Añadir archivo(s)...", OnAddFile);
        var addUrlButton = MakeButton("Añadir desde URL...", OnAddFromUrl);
        var importButton = MakeButton("Importar de la carpeta", OnImportFound);
        var detectButton = MakeButton("Detectar tipo", OnDetectType);
        var editButton = MakeButton("Editar...", OnEdit);
        var removeButton = MakeButton("Quitar", OnRemove);
        actionsPanel.Controls.AddRange(new Control[] { addFileButton, addUrlButton, importButton, detectButton, editButton, removeButton });
        root.Controls.Add(actionsPanel, 0, 1);

        // Row 2: grid
        _grid = BuildGrid();
        root.Controls.Add(_grid, 0, 2);

        // Row 3: progress
        var progressPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8, 2, 8, 2) };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        progressPanel.Controls.Add(_progressBar, 0, 0);
        _progressLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(8, 4, 0, 0), Text = "Listo." };
        progressPanel.Controls.Add(_progressLabel, 1, 0);
        root.Controls.Add(progressPanel, 0, 3);

        // Row 4: log
        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(8, 2, 8, 2) };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logPanel.Controls.Add(new Label { Text = "Registro de instalación:", AutoSize = true }, 0, 0);
        _logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font(FontFamily.GenericMonospace, 9), BackColor = Color.Black, ForeColor = Color.Gainsboro };
        logPanel.Controls.Add(_logBox, 0, 1);
        root.Controls.Add(logPanel, 0, 4);

        // Row 5: install controls
        var installPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4), FlowDirection = FlowDirection.LeftToRight };
        _installSelectedButton = MakeButton("Instalar seleccionados", OnInstallSelected);
        _installAllButton = MakeButton("Instalar todo", OnInstallAll);
        _cancelInstallButton = MakeButton("Detener", OnCancelInstall);
        _cancelInstallButton.Enabled = false;
        _stopOnErrorCheck = new CheckBox { Text = "Detener si falla uno", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(16, 8, 0, 0) };
        installPanel.Controls.AddRange(new Control[] { _installSelectedButton, _installAllButton, _cancelInstallButton, _stopOnErrorCheck });
        root.Controls.Add(installPanel, 0, 5);

        _installService.Log += OnInstallServiceLog;

        Load += (_, _) => LoadInitialFolder();
        FormClosing += (_, _) => SaveSettings();
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
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Estado", Width = 140, ReadOnly = true });

        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnEdit(this, EventArgs.Empty); };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) SaveManifest(); };

        return grid;
    }

    private void LoadInitialFolder()
    {
        var settings = _settingsService.Load();
        if (!string.IsNullOrWhiteSpace(settings.LastFolder) && Directory.Exists(settings.LastFolder))
        {
            LoadFolder(settings.LastFolder);
        }
        _stopOnErrorCheck.Checked = settings.StopOnError;
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings { LastFolder = string.IsNullOrEmpty(_folder) ? null : _folder, StopOnError = _stopOnErrorCheck.Checked });
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
        _folderTextBox.Text = folder;
        RefreshGrid();
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

    private void OnBrowseFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona (o crea) la carpeta donde viven tus instaladores" };
        if (!string.IsNullOrEmpty(_folder)) dialog.SelectedPath = _folder;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadFolder(dialog.SelectedPath);
        }
    }

    private void OnOpenFolder(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _folder,
            UseShellExecute = true,
        });
    }

    private void OnAddFile(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

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

        using var editForm = new EditInstallerForm(entry);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
    }

    private void OnAddFromUrl(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

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

        using var editForm = new EditInstallerForm(entry);
        editForm.ShowDialog(this);

        _manifest.Items.Add(entry);
        SaveManifest();
        RefreshGrid();
    }

    private void OnImportFound(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

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
        if (!EnsureFolderSelected()) return;

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

        using var editForm = new EditInstallerForm(row.Entry);
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
            $"¿Quitar {rows.Count} elemento(s) de la lista? El archivo no se borrará del disco.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return;

        foreach (var row in rows)
        {
            _manifest.Items.RemoveAll(i => i.Id == row.Entry.Id);
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

    private async void OnInstallSelected(object? sender, EventArgs e) =>
        await RunInstallAsync(SelectedRowsOrAll().Where(r => r.Enabled).ToList());

    private async void OnInstallAll(object? sender, EventArgs e) =>
        await RunInstallAsync(GridRows.Where(r => r.Enabled).ToList());

    private void OnCancelInstall(object? sender, EventArgs e) => _installCts?.Cancel();

    private async Task RunInstallAsync(List<InstallerRow> rows)
    {
        if (_installing) return;
        if (!EnsureFolderSelected()) return;

        if (rows.Count == 0)
        {
            MessageBox.Show(this, "No hay instaladores habilitados para instalar.", "Nada que instalar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _installing = true;
        SetInstallControlsEnabled(false);
        _logBox.Clear();
        _progressBar.Value = 0;
        _progressLabel.Text = $"0 / {rows.Count}";
        foreach (var row in rows) row.Status = "Pendiente";

        _installCts = new CancellationTokenSource();
        var rowsById = rows.ToDictionary(r => r.Entry.Id);

        var progress = new Progress<InstallProgress>(p =>
        {
            if (rowsById.TryGetValue(p.Current.Id, out var row))
            {
                row.Status = p.Result is null ? "Instalando..." : DescribeOutcome(p.Result.Outcome);
            }

            _progressBar.Value = (int)Math.Round(p.Total == 0 ? 0 : p.Completed * 100.0 / p.Total);
            _progressLabel.Text = $"{p.Completed} / {p.Total} - {p.Current.Name}";
        });

        try
        {
            var results = await _installService.InstallBatchAsync(
                _folder, rows.Select(r => r.Entry), _stopOnErrorCheck.Checked, progress, _installCts.Token);

            var succeeded = results.Count(r => r.Outcome is InstallOutcome.Success or InstallOutcome.SuccessRebootRequired);
            var rebootNeeded = results.Any(r => r.Outcome == InstallOutcome.SuccessRebootRequired);
            AppendLog($"--- Terminado: {succeeded}/{results.Count} correctos.{(rebootNeeded ? " Algunos requieren reiniciar." : "")} ---");
            _progressLabel.Text = $"Terminado: {succeeded}/{results.Count} correctos.";
        }
        catch (OperationCanceledException)
        {
            AppendLog("--- Instalación cancelada por el usuario. ---");
            _progressLabel.Text = "Cancelado.";
        }
        finally
        {
            _installCts.Dispose();
            _installCts = null;
            _installing = false;
            SetInstallControlsEnabled(true);
        }
    }

    private static string DescribeOutcome(InstallOutcome outcome) => outcome switch
    {
        InstallOutcome.Success => "OK",
        InstallOutcome.SuccessRebootRequired => "OK (reiniciar)",
        InstallOutcome.Failed => "Error",
        InstallOutcome.Cancelled => "Cancelado",
        InstallOutcome.FileNotFound => "Archivo no encontrado",
        _ => outcome.ToString(),
    };

    private void SetInstallControlsEnabled(bool enabled)
    {
        _installSelectedButton.Enabled = enabled;
        _installAllButton.Enabled = enabled;
        _cancelInstallButton.Enabled = !enabled;
    }

    private void OnInstallServiceLog(object? sender, InstallLogEventArgs e) => AppendLog(e.Message);

    private void AppendLog(string message)
    {
        if (_logBox.InvokeRequired)
        {
            _logBox.BeginInvoke(new Action(() => AppendLog(message)));
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }
}
