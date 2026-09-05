using System.ComponentModel;
using MegaInstaller.App.Dialogs;
using MegaInstaller.App.Theming;
using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App;

/// <summary>
/// Home screen: instances ("packs") come first, since that's what
/// MegaInstaller is built around. The full installer library (add/edit/
/// remove individual programs) lives one click away, in
/// <see cref="InstallerLibraryForm"/>.
///
/// Renders one of two ways depending on <see cref="AppTheme.IsModern"/>:
/// a classic DataGridView, or a card gallery (<see cref="_cardsFlow"/>).
/// Exactly one of <see cref="_grid"/>/<see cref="_cardsFlow"/> is built,
/// matching the theme in effect for this whole run.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ManifestService _manifestService = new();
    private readonly AppSettingsService _settingsService = new(AppSettingsService.DefaultPath);

    private InstallerManifest _manifest = new();
    private string _folder = string.Empty;
    private string? _selectedInstanceId;

    private readonly DataGridView? _grid;
    private readonly FlowLayoutPanel? _cardsFlow;
    private readonly ToolTip _toolTip = new();
    private bool _updateAvailable;

    public MainForm()
    {
        var elevated = ElevationProbe.IsProcessElevated();
        UpdateTitle();
        Width = 900;
        Height = 620;
        // Fixed size rather than resizable: the header and card gallery
        // aren't laid out to reflow at arbitrary widths, and a shrunk
        // window clipped controls instead of adapting to them.
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = LoadAppIcon();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        // Button rows need the button's full footprint (its height plus its
        // top/bottom margins) or the FlowLayoutPanel clips it at the bottom.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10, 6, 10, 6),
            BackColor = AppTheme.IsModern ? ModernPalette.Surface : Color.FromArgb(240, 243, 247),
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var appIcon = InstanceIconCatalog.Load("box-seam-fill");
        if (appIcon is not null)
        {
            headerPanel.Controls.Add(new PictureBox
            {
                Image = appIcon,
                Width = 28,
                Height = 28,
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 8, 0),
            }, 0, 0);
        }

        // Title and the admin marker share one font and sit in a single
        // left-anchored strip, so they're on the same baseline as each other
        // and vertically centred against the icon and the Ajustes button.
        var titleFont = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        var titlePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        };
        titlePanel.Controls.Add(new Label
        {
            Text = "MegaInstaller",
            AutoSize = true,
            Font = titleFont,
            ForeColor = AppTheme.IsModern ? ModernPalette.TextPrimary : SystemColors.ControlText,
            Margin = new Padding(0),
        });
        if (elevated)
        {
            titlePanel.Controls.Add(new Label
            {
                Text = "-",
                AutoSize = true,
                Font = titleFont,
                ForeColor = AppTheme.IsModern ? ModernPalette.TextSecondary : SystemColors.ControlText,
                Margin = new Padding(3, 0, 3, 0),
            });
            titlePanel.Controls.Add(new Label
            {
                Text = "AdminMode",
                AutoSize = true,
                Font = titleFont,
                ForeColor = ModernPalette.AdminGold,
                Margin = new Padding(0),
            });
        }
        headerPanel.Controls.Add(titlePanel, 1, 0);
        var settingsButton = MakeButton("Ajustes", OnOpenSettings, icon: InstanceIconCatalog.Load("gear-fill"));
        settingsButton.Anchor = AnchorStyles.Right;
        _toolTip.SetToolTip(settingsButton, "Cambiar la carpeta de instaladores y el aspecto de la app");
        headerPanel.Controls.Add(settingsButton, 2, 0);
        root.Controls.Add(headerPanel, 0, 0);

        var actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2) };

        // Nueva/Editar/Eliminar are all "manage the selected instance" actions,
        // so they live behind one dropdown instead of three separate buttons -
        // that's also what leaves room in this bar for "Instalar varias...".
        var instanceMenu = new ContextMenuStrip();
        instanceMenu.Items.Add(AppTheme.CreateMenuItem("Nueva instancia...", OnNewInstance));
        instanceMenu.Items.Add(AppTheme.CreateMenuItem("Editar instancia...", OnEditInstance));
        instanceMenu.Items.Add(AppTheme.CreateMenuItem("Eliminar instancia", OnRemoveInstance));
        actionsPanel.Controls.Add(AppTheme.CreateDropdownButton("Instancia", instanceMenu));

        actionsPanel.Controls.Add(MakeDivider());

        var installMenu = new ContextMenuStrip();
        installMenu.Items.Add(AppTheme.CreateMenuItem("Instalar seleccionada...", OnInstallInstance));
        installMenu.Items.Add(AppTheme.CreateMenuItem("Instalar varias...", OnInstallMultiple));
        actionsPanel.Controls.Add(AppTheme.CreateDropdownButton("Instalar", installMenu, primary: true));

        actionsPanel.Controls.Add(MakeDivider());

        actionsPanel.Controls.Add(MakeButton("Editor de programas...", OnOpenLibrary));

        actionsPanel.Controls.Add(MakeDivider());

        var exportMenu = new ContextMenuStrip();
        exportMenu.Items.Add(AppTheme.CreateMenuItem("Exportar instancia seleccionada...", OnExportInstance));
        exportMenu.Items.Add(AppTheme.CreateMenuItem("Exportar todo...", OnExportAll));
        exportMenu.Items.Add(AppTheme.CreateMenuItem("Importar...", OnImportPackage));
        actionsPanel.Controls.Add(AppTheme.CreateDropdownButton("Exportar/Importar", exportMenu));

        root.Controls.Add(actionsPanel, 0, 1);

        if (AppTheme.IsModern)
        {
            _cardsFlow = BuildCardsHost();
            root.Controls.Add(_cardsFlow, 0, 2);
        }
        else
        {
            _grid = BuildGrid();
            root.Controls.Add(_grid, 0, 2);
        }

        AppTheme.StyleForm(this);
        Load += (_, _) =>
        {
            EnsureFolderForThisSession();
            _ = CheckForUpdateInBackgroundAsync();
        };
    }

    private void UpdateTitle()
    {
        var parts = new List<string> { "MegaInstaller" };
        if (ElevationProbe.IsProcessElevated())
        {
            parts.Add("Administrador");
        }

        if (_updateAvailable)
        {
            parts.Add("Actualización disponible");
        }

        Text = string.Join(" - ", parts);
    }

    /// <summary>
    /// Silent, best-effort check against GitHub Releases: only ever changes
    /// the title bar when a genuinely newer version is confirmed, and never
    /// surfaces an error if GitHub is unreachable - the whole point is to be
    /// a free bonus on top of a normal launch, not something to depend on.
    /// </summary>
    private async Task CheckForUpdateInBackgroundAsync()
    {
        try
        {
            using var releaseService = new GitHubReleaseService();
            var latest = await releaseService.GetLatestReleaseAsync(ReleaseInfo.RepoOwner, ReleaseInfo.RepoName, CancellationToken.None);
            if (latest is not null && ReleaseInfo.IsNewer(latest.TagName))
            {
                _updateAvailable = true;
                UpdateTitle();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
        }
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

    private static Button MakeButton(string text, EventHandler handler, bool primary = false, Image? icon = null)
    {
        var button = AppTheme.CreateButton(text, primary, icon);
        button.Click += handler;
        return button;
    }

    /// <summary>A thin vertical rule separating groups of actions in the top bar - inset top/bottom so it reads as deliberate, not a stray line.</summary>
    private static Control MakeDivider() => new Panel
    {
        Width = 1,
        Height = 28,
        Margin = new Padding(6, 7, 6, 7),
        BackColor = AppTheme.IsModern ? ModernPalette.Border : SystemColors.ControlDark,
    };

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

        // Double-click installs the selected instance; editing has its own
        // explicit menu entry. The row is already selected by the first of
        // the two clicks (FullRowSelect selects on mouse-down).
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OnInstallInstance(this, EventArgs.Empty); };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) SaveManifest(); };

        return grid;
    }

    private FlowLayoutPanel BuildCardsHost() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = ModernPalette.Background,
        Padding = new Padding(12),
    };

    /// <summary>
    /// Shows the folder picker once per Windows logon session (tracked via
    /// Process.SessionId) rather than on every single launch - except that a
    /// missing/invalid folder (deleted, a disconnected drive, ...) always
    /// forces the picker regardless of the session gate, since there's
    /// nothing usable to silently fall back to. Choosing "Salir" there
    /// closes the whole app instead of leaving Home half set up.
    /// </summary>
    private void EnsureFolderForThisSession()
    {
        var settings = _settingsService.Load();
        var currentSessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        var folderIsValid = !string.IsNullOrWhiteSpace(settings.LastFolder) && Directory.Exists(settings.LastFolder);

        if (settings.LastWindowsSessionId != currentSessionId || !folderIsValid)
        {
            using var startupForm = new SelectFolderStartupForm(folderIsValid ? settings.LastFolder : null);
            if (startupForm.ShowDialog(this) != DialogResult.OK || startupForm.SelectedFolder is null)
            {
                Application.Exit();
                return;
            }

            settings.LastFolder = startupForm.SelectedFolder;
            settings.LastWindowsSessionId = currentSessionId;
            _settingsService.Save(settings);
        }

        LoadFolder(settings.LastFolder!);
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
        _selectedInstanceId = null;

        var settings = _settingsService.Load();
        settings.LastFolder = folder;
        _settingsService.Save(settings);

        RefreshGrid();
    }

    private void RefreshGrid()
    {
        if (AppTheme.IsModern)
        {
            RefreshCards();
            return;
        }

        var rows = new BindingList<InstanceRow>(
            _manifest.Instances
                .OrderBy(i => i.Order)
                .Select(i => new InstanceRow(i, InstanceService.ResolveInstallers(_manifest, i).Count, _folder))
                .ToList());
        _grid!.DataSource = rows;
    }

    private void RefreshCards()
    {
        var flow = _cardsFlow!;
        flow.SuspendLayout();
        flow.Controls.Clear();

        foreach (var instance in _manifest.Instances.OrderBy(i => i.Order))
        {
            var resolved = InstanceService.ResolveInstallers(_manifest, instance);
            var hasWebInstallers = resolved.Any(entry => !string.IsNullOrWhiteSpace(entry.MirrorUrl));
            var card = InstanceCardControl.ForInstance(instance, resolved.Count, hasWebInstallers, _folder);
            card.Selected = card.InstanceId == _selectedInstanceId;
            card.Click += (_, _) => SelectCard(card.InstanceId);
            // Double-click installs; editing has its own explicit menu entry.
            card.DoubleClick += (_, _) => { SelectCard(card.InstanceId); OnInstallInstance(this, EventArgs.Empty); };
            flow.Controls.Add(card);
        }

        var addCard = InstanceCardControl.CreateAddTile();
        addCard.Click += (_, _) => OnNewInstance(this, EventArgs.Empty);
        flow.Controls.Add(addCard);

        flow.ResumeLayout();
    }

    private void SelectCard(string? instanceId)
    {
        _selectedInstanceId = instanceId;
        foreach (var card in _cardsFlow!.Controls.OfType<InstanceCardControl>())
        {
            card.Selected = card.InstanceId == instanceId;
        }
    }

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

    private InstanceRow? SelectedRow()
    {
        if (AppTheme.IsModern)
        {
            var instance = _selectedInstanceId is null
                ? null
                : _manifest.Instances.FirstOrDefault(i => i.Id == _selectedInstanceId);
            return instance is null ? null : new InstanceRow(instance, InstanceService.ResolveInstallers(_manifest, instance).Count, _folder);
        }

        return _grid!.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as InstanceRow).FirstOrDefault();
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        var themeBefore = AppTheme.Current;
        var settingsBefore = _settingsService.Load();
        using var settingsForm = new SettingsForm(_folder, themeBefore, settingsBefore);
        settingsForm.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(settingsForm.SelectedFolder) &&
            !string.Equals(settingsForm.SelectedFolder, _folder, StringComparison.OrdinalIgnoreCase))
        {
            LoadFolder(settingsForm.SelectedFolder);
        }

        // Re-read rather than reusing settingsBefore: LoadFolder above may
        // have written the newly picked folder to the same file.
        var settings = _settingsService.Load();
        settings.TroubleshooterEnabled = settingsForm.TroubleshooterEnabled;
        settings.SkipElevationOffer = settingsForm.SkipElevationOffer;
        settings.UiTheme = settingsForm.SelectedTheme;
        settings.WebCacheFolder = settingsForm.WebCacheFolder;
        settings.ClearWebCacheAfterInstall = settingsForm.ClearWebCacheAfterInstall;
        _settingsService.Save(settings);

        if (settingsForm.SelectedTheme != themeBefore)
        {
            var restart = MessageBox.Show(this,
                "El nuevo aspecto se aplicará al reiniciar MegaInstaller. ¿Reiniciar ahora?",
                "Reinicio necesario", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (restart == DialogResult.Yes)
            {
                Application.Restart();
            }
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
        using var editForm = new EditInstanceForm(instance, _manifest.Items, _folder);
        if (editForm.ShowDialog(this) != DialogResult.OK) return;

        _manifest.Instances.Add(instance);
        _selectedInstanceId = instance.Id;
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

        using var editForm = new EditInstanceForm(row.Instance, _manifest.Items, _folder);
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
        if (_selectedInstanceId == row.Instance.Id) _selectedInstanceId = null;
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

    private void OnInstallMultiple(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        if (_manifest.Instances.Count == 0)
        {
            MessageBox.Show(this, "Todavía no hay ninguna instancia creada.", "Nada que instalar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var multiForm = new MultiInstallInstancesForm(_folder, _manifest);
        multiForm.ShowDialog(this);
    }

    private void OnExportInstance(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        var row = SelectedRow();
        if (row is null)
        {
            MessageBox.Show(this, "Selecciona una instancia para exportar.", "Nada seleccionado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Exportar instancia",
            Filter = "Paquete de MegaInstaller (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
            FileName = $"{SanitizeFileName(row.Instance.Name)}.zip",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ExportPackageService.ExportInstance(_folder, _manifest, row.Instance, dialog.FileName);
            MessageBox.Show(this, $"Instancia exportada a:\n{dialog.FileName}", "Exportación completada",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"No se pudo exportar: {ex.Message}", "Error al exportar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportAll(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Exportar todo",
            Filter = "Paquete de MegaInstaller (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
            FileName = "MegaInstaller-completo.zip",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ExportPackageService.ExportAll(_folder, _manifest, dialog.FileName);
            MessageBox.Show(this, $"Todo exportado a:\n{dialog.FileName}", "Exportación completada",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"No se pudo exportar: {ex.Message}", "Error al exportar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnImportPackage(object? sender, EventArgs e)
    {
        if (!EnsureFolderSelected()) return;

        using var dialog = new OpenFileDialog
        {
            Title = "Importar paquete de MegaInstaller",
            Filter = "Paquete de MegaInstaller (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        ImportPreview preview;
        try
        {
            preview = ExportPackageService.PreviewImport(dialog.FileName, _folder);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"No se pudo leer el paquete: {ex.Message}", "Paquete no válido",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (preview.NewInstances.Count == 0 && preview.NewInstallers.Count == 0)
        {
            MessageBox.Show(this,
                "No hay nada nuevo que importar: todo lo que trae este paquete ya está en esta carpeta.",
                "Nada que importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var summary = $"Se añadirán {preview.NewInstances.Count} instancia(s) y {preview.NewInstallers.Count} programa(s).";
        if (preview.SkippedInstances.Count > 0 || preview.SkippedInstallers.Count > 0)
        {
            summary += $"\nYa existían y se omitirán: {preview.SkippedInstances.Count} instancia(s), {preview.SkippedInstallers.Count} programa(s).";
        }
        if (preview.RenamedFiles.Count > 0)
        {
            summary += $"\nSe renombrarán por conflicto de nombre: {string.Join(", ", preview.RenamedFiles)}.";
        }
        summary += "\n\n¿Continuar con la importación?";

        var choice = MessageBox.Show(this, summary, "Importar paquete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return;

        try
        {
            ExportPackageService.Import(dialog.FileName, _folder);
            LoadFolder(_folder);
            MessageBox.Show(this, "Importación completada.", "Importar paquete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"No se pudo importar: {ex.Message}", "Error al importar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "instancia" : sanitized;
    }
}
