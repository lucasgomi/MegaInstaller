using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Downloads an installer straight into the selected folder, showing live
/// progress, then hands the caller the resulting file name so it can be
/// registered in the manifest.
/// </summary>
public sealed class AddFromUrlForm : Form
{
    private readonly string _destinationFolder;
    private readonly DownloadService _downloadService = new();
    private CancellationTokenSource? _cts;

    private readonly TextBox _urlBox;
    private readonly TextBox _nameBox;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly Button _downloadButton;
    private readonly Button _closeButton;

    public string? DownloadedFileName { get; private set; }
    public string SuggestedName => _nameBox.Text.Trim();
    public string Url => _urlBox.Text.Trim();

    public AddFromUrlForm(string destinationFolder)
    {
        _destinationFolder = destinationFolder;

        Text = "Añadir instalador desde URL";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 226);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 5,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // RowStyles is positional (RowStyles[i] = row i); declare all of
        // them upfront so none fall back to an unpredictable default.
        int[] rowHeights = { 34, 34, 28, 28, 46 };
        foreach (var height in rowHeights)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "URL:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _urlBox = new TextBox { Dock = DockStyle.Fill };
        _urlBox.TextChanged += (_, _) => SuggestNameFromUrl();
        layout.Controls.Add(_urlBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _nameBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_nameBox, 1, 1);

        _progressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        layout.Controls.Add(_progressBar, 1, 2);

        _statusLabel = new Label { Dock = DockStyle.Fill, AutoSize = false, Text = "Listo para descargar." };
        layout.Controls.Add(_statusLabel, 1, 3);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _closeButton = AppTheme.CreateButton("Cerrar");
        _closeButton.DialogResult = DialogResult.Cancel;
        _downloadButton = AppTheme.CreateButton("Descargar", primary: true);
        _downloadButton.Click += OnDownloadClick;
        buttonsPanel.Controls.Add(_closeButton);
        buttonsPanel.Controls.Add(_downloadButton);
        layout.Controls.Add(buttonsPanel, 1, 4);

        CancelButton = _closeButton;
        FormClosing += (_, _) => _cts?.Cancel();
        AppTheme.StyleForm(this);
    }

    private void SuggestNameFromUrl()
    {
        if (!Uri.TryCreate(_urlBox.Text.Trim(), UriKind.Absolute, out var uri))
        {
            return;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _nameBox.Text = Path.GetFileNameWithoutExtension(fileName);
        }
    }

    private async void OnDownloadClick(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(_urlBox.Text.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "Introduce una URL http(s) válida.", "URL no válida",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show(this, "No se pudo determinar el nombre de archivo a partir de la URL.",
                "Nombre de archivo desconocido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var destinationPath = Path.Combine(_destinationFolder, fileName);
        if (File.Exists(destinationPath))
        {
            var overwrite = MessageBox.Show(this,
                $"Ya existe \"{fileName}\" en la carpeta. ¿Sobrescribir?",
                "El archivo ya existe", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (overwrite != DialogResult.Yes)
            {
                return;
            }
        }

        _urlBox.Enabled = false;
        _nameBox.Enabled = false;
        _downloadButton.Enabled = false;
        _cts = new CancellationTokenSource();

        var progress = new Progress<DownloadProgressInfo>(info =>
        {
            if (info.PercentComplete is { } percent)
            {
                _progressBar.Value = (int)percent;
                _statusLabel.Text = $"{percent:0.#}% - {FormatBytes(info.BytesReceived)} - {FormatBytes((long)info.BytesPerSecond)}/s";
            }
            else
            {
                _statusLabel.Text = $"{FormatBytes(info.BytesReceived)} descargados - {FormatBytes((long)info.BytesPerSecond)}/s";
            }
        });

        try
        {
            await _downloadService.DownloadAsync(uri, destinationPath, progress, _cts.Token);
            DownloadedFileName = fileName;
            _statusLabel.Text = "Descarga completada.";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Descarga cancelada.";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _statusLabel.Text = "Error de descarga.";
            MessageBox.Show(this, ex.Message, "No se pudo descargar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _urlBox.Enabled = true;
            _nameBox.Enabled = true;
            _downloadButton.Enabled = true;
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _downloadService.Dispose();
        }

        base.Dispose(disposing);
    }
}
