using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Registers a web-sourced installer: only its mirror URL and install
/// config are stored, never a local file (see <see cref="InstallerEntry.MirrorUrl"/>).
/// "Comprobar mirror ahora" is optional - it downloads to a throwaway temp
/// file just to confirm the link works and to compute a hash worth pinning,
/// then deletes it; nothing here ever touches the installers folder.
/// </summary>
public sealed class AddWebInstallerForm : Form
{
    private readonly TextBox _nameBox;
    private readonly TextBox _urlBox;
    private readonly TextBox _fileNameBox;
    private readonly ComboBox _typeCombo;
    private readonly Button _checkButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly CheckBox _pinHashCheck;
    private readonly Button _addButton;

    private string? _verifiedHash;
    private CancellationTokenSource? _cts;
    private bool _typeManuallySet;
    private bool _isAutoDetecting;
    private Image? _extractedIcon;

    public string EntryName => _nameBox.Text.Trim();
    public string MirrorUrl => _urlBox.Text.Trim();
    public string FileName => EffectiveFileName() ?? string.Empty;
    public InstallerType SelectedType =>
        Enum.TryParse<InstallerType>(_typeCombo.SelectedItem as string, out var type) ? type : InstallerType.Unknown;
    public string? PinnedSha256 => _pinHashCheck.Enabled && _pinHashCheck.Checked ? _verifiedHash : null;

    /// <summary>
    /// The icon extracted from the mirror's real bytes during "Comprobar
    /// mirror ahora", if any - an in-memory copy that survives the temp
    /// file's own cleanup, for the caller to save under CustomTheme once it
    /// knows which instance(s) this entry ends up belonging to.
    /// </summary>
    public Image? ExtractedIcon => _extractedIcon;

    public AddWebInstallerForm()
    {
        Text = "Añadir instalador web";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 340);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // A Label+TextBox/ComboBox row needs 34px in this app (see
        // AddFromUrlForm/EditInstallerForm) - anything less clips or
        // crowds it against the row above/below.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Nombre
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // URL del mirror
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Nombre de archivo
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Tipo
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Comprobar mirror ahora
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // progress bar
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // status text + pin-hash checkbox
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // buttons
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _nameBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_nameBox, 1, 0);

        layout.Controls.Add(new Label { Text = "URL del mirror:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _urlBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Enlace de descarga directa (.exe, .msi, ...)" };
        _urlBox.TextChanged += (_, _) => { SuggestFromUrl(); ResetVerification(); };
        layout.Controls.Add(_urlBox, 1, 1);

        layout.Controls.Add(new Label { Text = "Nombre de archivo:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _fileNameBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Se deduce de la URL si lo dejas vacío" };
        layout.Controls.Add(_fileNameBox, 1, 2);

        layout.Controls.Add(new Label { Text = "Tipo:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        _typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _typeCombo.Items.AddRange(Enum.GetNames<InstallerType>());
        _typeCombo.SelectedItem = InstallerType.Unknown.ToString();
        // Once the user picks a type themselves, auto-detection from the
        // URL/file name must stop overwriting it - only a programmatic
        // change (guarded by _isAutoDetecting) doesn't count as "manual".
        _typeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_isAutoDetecting)
            {
                _typeManuallySet = true;
            }
        };
        layout.Controls.Add(_typeCombo, 1, 3);

        layout.Controls.Add(new Label(), 0, 4);
        _checkButton = AppTheme.CreateButton("Comprobar mirror ahora");
        _checkButton.Dock = DockStyle.Fill;
        _checkButton.Click += OnCheckMirror;
        layout.Controls.Add(_checkButton, 1, 4);

        layout.Controls.Add(new Label(), 0, 5);
        _progressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Visible = false };
        layout.Controls.Add(_progressBar, 1, 5);

        layout.Controls.Add(new Label(), 0, 6);
        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true };
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _statusLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) };
        _pinHashCheck = new CheckBox { Text = "Fijar el hash comprobado (recomendado)", AutoSize = true, Enabled = false };
        statusPanel.Controls.Add(_statusLabel, 0, 0);
        statusPanel.Controls.Add(_pinHashCheck, 0, 1);
        layout.Controls.Add(statusPanel, 1, 6);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        _addButton = AppTheme.CreateButton("Añadir", primary: true);
        _addButton.Click += OnAddClick;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(_addButton);
        layout.Controls.Add(buttonsPanel, 0, 7);
        layout.SetColumnSpan(buttonsPanel, 2);

        CancelButton = cancelButton;
        FormClosing += (_, _) => _cts?.Cancel();
        AppTheme.StyleForm(this);
    }

    private void SuggestFromUrl()
    {
        if (!Uri.TryCreate(_urlBox.Text.Trim(), UriKind.Absolute, out var uri))
        {
            return;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            _nameBox.Text = Path.GetFileNameWithoutExtension(fileName);
        }

        if (string.IsNullOrWhiteSpace(_fileNameBox.Text))
        {
            _fileNameBox.Text = fileName;
        }

        // Detect() is extension-authoritative for package formats and falls
        // back to Unknown for anything it would otherwise need the actual
        // bytes to sniff - exactly right here, since there's no file yet.
        // Never overrides a type the user picked themselves.
        if (_typeManuallySet)
        {
            return;
        }

        var detected = InstallerTypeDetector.Detect(fileName);
        if (detected != InstallerType.Unknown)
        {
            _isAutoDetecting = true;
            _typeCombo.SelectedItem = detected.ToString();
            _isAutoDetecting = false;
        }
    }

    private void ResetVerification()
    {
        _verifiedHash = null;
        _pinHashCheck.Checked = false;
        _pinHashCheck.Enabled = false;
        _statusLabel.Text = string.Empty;
    }

    private string? EffectiveFileName()
    {
        if (!string.IsNullOrWhiteSpace(_fileNameBox.Text))
        {
            return _fileNameBox.Text.Trim();
        }

        return Uri.TryCreate(_urlBox.Text.Trim(), UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.LocalPath) is { Length: > 0 } name ? name : null
            : null;
    }

    private bool TryGetValidUri(out Uri uri)
    {
        if (Uri.TryCreate(_urlBox.Text.Trim(), UriKind.Absolute, out uri!) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        MessageBox.Show(this, "Introduce una URL http(s) válida.", "URL no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private async void OnCheckMirror(object? sender, EventArgs e)
    {
        if (!TryGetValidUri(out var uri))
        {
            return;
        }

        var fileName = EffectiveFileName();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show(this, "No se pudo determinar un nombre de archivo. Indícalo manualmente.",
                "Falta el nombre de archivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var tempDir = Directory.CreateTempSubdirectory("megainstaller-mirrorcheck-").FullName;
        _checkButton.Enabled = false;
        _addButton.Enabled = false;
        _progressBar.Value = 0;
        _progressBar.Visible = true;
        _statusLabel.ForeColor = SystemColors.GrayText;
        _statusLabel.Text = "Comprobando...";
        _cts = new CancellationTokenSource();

        try
        {
            using var downloadService = new DownloadService();
            var destinationPath = Path.Combine(tempDir, fileName);
            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                if (info.PercentComplete is { } percent)
                {
                    _progressBar.Value = Math.Clamp((int)percent, 0, 100);
                }
            });

            await downloadService.DownloadAsync(uri, destinationPath, progress, _cts.Token);

            var info = new FileInfo(destinationPath);
            if (!info.Exists || info.Length == 0)
            {
                _statusLabel.Text = "El mirror devolvió un archivo vacío.";
                return;
            }

            _verifiedHash = await Sha256Service.ComputeAsync(destinationPath, _cts.Token);
            _statusLabel.Text = $"Mirror accesible. Tamaño: {FormatBytes(info.Length)}.";
            _pinHashCheck.Enabled = true;
            _pinHashCheck.Checked = true;

            // Best-effort: extracted from the PE resource section directly,
            // so the file's overall size doesn't matter. Reads into an
            // independent in-memory bitmap (see IconExtractor), so it
            // survives this method's own temp-file cleanup below.
            _extractedIcon?.Dispose();
            _extractedIcon = IconExtractor.TryExtract(destinationPath);

            // Only extension-based detection was possible before this point
            // (there was no file yet); now the real bytes are on disk, so a
            // byte-marker family (Inno/NSIS/Squirrel/...) can actually be
            // sniffed - same rule as always: never overrides a manual pick.
            if (!_typeManuallySet)
            {
                var detected = InstallerTypeDetector.Detect(destinationPath);
                if (detected != InstallerType.Unknown)
                {
                    _isAutoDetecting = true;
                    _typeCombo.SelectedItem = detected.ToString();
                    _isAutoDetecting = false;
                    _statusLabel.Text += $" Tipo detectado: {detected}.";
                }
            }
        }
        catch (OperationCanceledException) when (_cts?.Token.IsCancellationRequested == true)
        {
            _statusLabel.Text = "Comprobación cancelada.";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // OperationCanceledException lands here too when it wasn't the
            // user's own token (e.g. a stalled connection) - see
            // DownloadService's comment on why that's no longer a timeout,
            // but staying defensive here means it's reported honestly
            // instead of being mislabeled as "cancelada" like before.
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = $"No se pudo comprobar: {ex.Message}";
        }
        finally
        {
            _checkButton.Enabled = true;
            _addButton.Enabled = true;
            _progressBar.Visible = false;
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void OnAddClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "El nombre no puede estar vacío.", "Falta el nombre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryGetValidUri(out _))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EffectiveFileName()))
        {
            MessageBox.Show(this, "Indica un nombre de archivo (con extensión, p. ej. setup.exe).",
                "Falta el nombre de archivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
