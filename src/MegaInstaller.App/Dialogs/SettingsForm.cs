using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>Where the installers folder (previously on the Home screen) now lives, plus the app's look.</summary>
public sealed class SettingsForm : Form
{
    private readonly TextBox _folderTextBox;
    private readonly RadioButton _classicRadio;
    private readonly RadioButton _modernRadio;
    private readonly CheckBox _troubleshooterCheck;
    private readonly CheckBox _elevationOfferCheck;
    private readonly TextBox _webCacheFolderTextBox;
    private readonly CheckBox _clearWebCacheCheck;
    private readonly Button _checkUpdateButton;
    private readonly Button _changelogButton;
    private readonly Button _installUpdateButton;
    private readonly Label _updateStatusLabel;
    private GitHubReleaseInfo? _lastCheckedRelease;
    private CancellationTokenSource? _updateCts;

    public string SelectedFolder { get; private set; }

    public UiThemeMode SelectedTheme => _modernRadio.Checked ? UiThemeMode.Modern : UiThemeMode.Classic;

    public bool TroubleshooterEnabled => _troubleshooterCheck.Checked;

    public bool SkipElevationOffer => !_elevationOfferCheck.Checked;

    public string? WebCacheFolder => string.IsNullOrWhiteSpace(_webCacheFolderTextBox.Text) ? null : _webCacheFolderTextBox.Text.Trim();

    public bool ClearWebCacheAfterInstall => _clearWebCacheCheck.Checked;

    public SettingsForm(string currentFolder, UiThemeMode currentTheme, AppSettings settings)
    {
        SelectedFolder = currentFolder;

        Text = "Ajustes";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 568);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 14 };
        // RowStyles is positional (RowStyles[i] = row i); declare all of
        // them upfront so none fall back to an unpredictable default.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        // Button rows need the button's whole footprint (its height plus
        // its own top/bottom margins) - 44 is the height proven to work for
        // every other single-button row in this same form (see above); a
        // narrower row clips the button instead of fitting around it.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        // Taller than a plain button row: the status text can run up to a
        // couple of wrapped lines (e.g. the "no se encontró el .exe..." message).
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "Carpeta de instaladores:", AutoSize = true }, 0, 0);

        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = currentFolder };
        var browseButton = AppTheme.CreateButton("Examinar...");
        browseButton.Margin = new Padding(6, 0, 0, 0);
        browseButton.Click += OnBrowse;
        var openButton = AppTheme.CreateButton("Abrir carpeta");
        openButton.Margin = new Padding(6, 0, 0, 0);
        openButton.Click += OnOpenFolder;
        folderPanel.Controls.Add(_folderTextBox, 0, 0);
        folderPanel.Controls.Add(browseButton, 1, 0);
        folderPanel.Controls.Add(openButton, 2, 0);
        root.Controls.Add(folderPanel, 0, 1);

        root.Controls.Add(new Label { Text = "Interfaz:", AutoSize = true }, 0, 2);

        var themePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _classicRadio = new RadioButton { Text = "Clásica", AutoSize = true, Checked = currentTheme == UiThemeMode.Classic };
        _modernRadio = new RadioButton { Text = "Moderna (beta)", AutoSize = true, Checked = currentTheme == UiThemeMode.Modern, Margin = new Padding(16, 0, 0, 0) };
        themePanel.Controls.Add(_classicRadio);
        themePanel.Controls.Add(_modernRadio);
        root.Controls.Add(themePanel, 0, 3);

        _troubleshooterCheck = new CheckBox
        {
            Text = "Activar el diagnóstico de instalación (troubleshooter)",
            AutoSize = true,
            Checked = settings.TroubleshooterEnabled,
        };
        root.Controls.Add(_troubleshooterCheck, 0, 4);

        _elevationOfferCheck = new CheckBox
        {
            Text = "Ofrecer reiniciar como administrador para un solo aviso de UAC",
            AutoSize = true,
            Checked = !settings.SkipElevationOffer,
        };
        root.Controls.Add(_elevationOfferCheck, 0, 5);

        root.Controls.Add(new Label { Text = "Caché de descargas web (avanzado):", AutoSize = true }, 0, 6);

        var webCachePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        webCachePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        webCachePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _webCacheFolderTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = settings.WebCacheFolder ?? string.Empty,
            PlaceholderText = "Vacío = carpeta predeterminada",
        };
        var browseWebCacheButton = AppTheme.CreateButton("Examinar...");
        browseWebCacheButton.Margin = new Padding(6, 0, 0, 0);
        browseWebCacheButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta para las descargas de instaladores web" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _webCacheFolderTextBox.Text = dialog.SelectedPath;
            }
        };
        webCachePanel.Controls.Add(_webCacheFolderTextBox, 0, 0);
        webCachePanel.Controls.Add(browseWebCacheButton, 1, 0);
        root.Controls.Add(webCachePanel, 0, 7);

        _clearWebCacheCheck = new CheckBox
        {
            Text = "Borrar la caché de descargas web al terminar cada instalación",
            AutoSize = true,
            Checked = settings.ClearWebCacheAfterInstall,
        };
        root.Controls.Add(_clearWebCacheCheck, 0, 8);

        var elevateNowButton = AppTheme.CreateButton("Reiniciar como administrador ahora");
        elevateNowButton.Enabled = !ElevationProbe.IsProcessElevated();
        elevateNowButton.Click += (_, _) => ElevatedRelauncher.TryRelaunchElevated(this);
        root.Controls.Add(elevateNowButton, 0, 9);

        var updateActionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _checkUpdateButton = AppTheme.CreateButton("Buscar actualizaciones");
        _checkUpdateButton.Click += OnCheckForUpdates;
        _changelogButton = AppTheme.CreateButton("Ver novedades...");
        _changelogButton.Enabled = false;
        _changelogButton.Click += OnShowChangelog;
        var openRepoButton = AppTheme.CreateButton("Abrir en GitHub");
        openRepoButton.Click += (_, _) => OpenUrl($"https://github.com/{ReleaseInfo.RepoOwner}/{ReleaseInfo.RepoName}");
        updateActionsPanel.Controls.Add(_checkUpdateButton);
        updateActionsPanel.Controls.Add(_changelogButton);
        updateActionsPanel.Controls.Add(openRepoButton);
        root.Controls.Add(updateActionsPanel, 0, 10);

        var updateStatusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        updateStatusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        updateStatusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _updateStatusLabel = new Label
        {
            // Dock alone: WinForms' Anchor setter resets Dock to None as a
            // side effect, so also setting Anchor here (as this used to)
            // silently undid the Fill and left the label stuck at its tiny
            // ~100x23px AutoSize=false default - which is what clipped the
            // sentence instead of letting it fill the column and wrap.
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = SystemColors.GrayText,
            Text = "Pulsa \"Buscar actualizaciones\" para comprobar si hay una versión más reciente.",
        };
        _installUpdateButton = AppTheme.CreateButton("Instalar actualización", primary: true);
        _installUpdateButton.Enabled = false;
        _installUpdateButton.Click += OnInstallUpdate;
        updateStatusPanel.Controls.Add(_updateStatusLabel, 0, 0);
        updateStatusPanel.Controls.Add(_installUpdateButton, 1, 0);
        root.Controls.Add(updateStatusPanel, 0, 11);

        root.Controls.Add(new Label
        {
            Text = $"MegaInstaller {ReleaseInfo.CurrentVersion}" +
                   (ElevationProbe.IsProcessElevated() ? " - ejecutándose como administrador" : string.Empty),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 0),
        }, 0, 12);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = AppTheme.CreateButton("Cerrar");
        closeButton.DialogResult = DialogResult.OK;
        buttonsPanel.Controls.Add(closeButton);
        root.Controls.Add(buttonsPanel, 0, 13);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        FormClosing += (_, _) => _updateCts?.Cancel();
        AppTheme.StyleForm(this);
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona (o crea) la carpeta donde viven tus instaladores",
            ShowNewFolderButton = true,
            SelectedPath = SelectedFolder,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectedFolder = dialog.SelectedPath;
            _folderTextBox.Text = SelectedFolder;
        }
    }

    private void OnOpenFolder(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            MessageBox.Show(this, "Todavía no hay una carpeta válida seleccionada.", "Sin carpeta",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = SelectedFolder,
            UseShellExecute = true,
        });
    }

    private async void OnCheckForUpdates(object? sender, EventArgs e)
    {
        _checkUpdateButton.Enabled = false;
        _installUpdateButton.Enabled = false;
        _updateStatusLabel.ForeColor = SystemColors.GrayText;
        _updateStatusLabel.Text = "Comprobando...";

        try
        {
            using var releaseService = new GitHubReleaseService();
            _lastCheckedRelease = await releaseService.GetLatestReleaseAsync(ReleaseInfo.RepoOwner, ReleaseInfo.RepoName, CancellationToken.None);

            if (_lastCheckedRelease is null)
            {
                _updateStatusLabel.Text = "No se pudo comprobar (sin conexión, o GitHub no respondió).";
                return;
            }

            _changelogButton.Enabled = true;

            if (ReleaseInfo.IsNewer(_lastCheckedRelease.TagName))
            {
                _updateStatusLabel.Text = $"Hay una versión nueva: {_lastCheckedRelease.TagName}.";
                _installUpdateButton.Enabled = _lastCheckedRelease.ExeDownloadUrl is not null;
                if (_lastCheckedRelease.ExeDownloadUrl is null)
                {
                    _updateStatusLabel.Text += " (no se encontró el .exe en la release; instálala manualmente desde GitHub)";
                }
            }
            else
            {
                _updateStatusLabel.Text = $"Ya tienes la última versión ({ReleaseInfo.CurrentVersion}).";
            }
        }
        finally
        {
            _checkUpdateButton.Enabled = true;
        }
    }

    private void OnShowChangelog(object? sender, EventArgs e)
    {
        if (_lastCheckedRelease is null) return;

        using var changelogForm = new ChangelogForm(_lastCheckedRelease);
        changelogForm.ShowDialog(this);
    }

    private async void OnInstallUpdate(object? sender, EventArgs e)
    {
        if (_lastCheckedRelease?.ExeDownloadUrl is not { } exeUrl) return;

        var confirm = MessageBox.Show(this,
            $"Se descargará e instalará la versión {_lastCheckedRelease.TagName}. MegaInstaller se cerrará y se reiniciará automáticamente al terminar.\n\n¿Continuar?",
            "Instalar actualización", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _checkUpdateButton.Enabled = false;
        _installUpdateButton.Enabled = false;
        _updateCts = new CancellationTokenSource();

        var progress = new Progress<DownloadProgressInfo>(info =>
        {
            _updateStatusLabel.Text = info.PercentComplete is { } percent
                ? $"Descargando actualización... {percent:0.#}%"
                : "Descargando actualización...";
        });

        var started = await AppUpdater.DownloadAndInstallAsync(this, exeUrl, progress, _updateCts.Token);
        if (started)
        {
            Application.Exit();
            return;
        }

        _checkUpdateButton.Enabled = true;
        _installUpdateButton.Enabled = true;
        _updateStatusLabel.Text = $"Hay una versión nueva: {_lastCheckedRelease.TagName}.";
    }

    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            MessageBox.Show(this, $"No se pudo abrir el enlace: {ex.Message}", "No se pudo abrir",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
