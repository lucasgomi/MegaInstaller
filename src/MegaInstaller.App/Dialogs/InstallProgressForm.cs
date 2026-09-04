using System.ComponentModel;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Self-contained "run this batch" dialog: shows live per-item status, an
/// overall progress bar, and a real-time log while <see cref="InstallService"/>
/// installs the given entries. Used both by the installer library ("Instalar
/// seleccionados/todo") and by instance installs (easy/advanced), so the
/// install experience is identical either way.
/// </summary>
public sealed class InstallProgressForm : Form
{
    private readonly InstallService _installService = new();
    private readonly string _folder;
    private readonly List<InstallerEntry> _entries;
    private readonly bool _stopOnError;
    private CancellationTokenSource? _cts;
    private bool _installing;

    private readonly DataGridView _grid;
    private readonly BindingList<InstallerRow> _rows;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;
    private readonly RichTextBox _logBox;
    private readonly Button _cancelButton;
    private readonly Button _closeButton;

    public IReadOnlyList<InstallResult> Results { get; private set; } = Array.Empty<InstallResult>();

    public InstallProgressForm(string folder, IEnumerable<InstallerEntry> entries, bool stopOnError)
    {
        _folder = folder;
        _entries = entries.ToList();
        _stopOnError = stopOnError;

        Text = "Instalando...";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 560);
        MinimumSize = new Size(560, 400);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);

        _rows = new BindingList<InstallerRow>(_entries.Select(e => new InstallerRow(e, folder)).ToList());
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _rows,
        };
        GridStyle.Apply(_grid);
        var iconColumn = new DataGridViewImageColumn { DataPropertyName = "Icon", HeaderText = "", Width = 32, ImageLayout = DataGridViewImageCellLayout.Zoom };
        GridStyle.ApplyIconColumn(iconColumn);
        _grid.Columns.Add(iconColumn);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Nombre", Width = 210 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FileName", HeaderText = "Archivo", Width = 190 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "Tipo", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Estado", Width = 160 });
        _grid.RowTemplate.Height = 30;
        root.Controls.Add(_grid, 0, 0);

        var progressPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = Math.Max(1, _entries.Count) };
        progressPanel.Controls.Add(_progressBar, 0, 0);
        _progressLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(8, 4, 0, 0), Text = $"0 / {_entries.Count}" };
        progressPanel.Controls.Add(_progressLabel, 1, 0);
        root.Controls.Add(progressPanel, 0, 1);

        root.Controls.Add(new Label { Text = "Registro:", AutoSize = true, Margin = new Padding(0, 6, 0, 2) }, 0, 2);

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font(FontFamily.GenericMonospace, 9),
            BackColor = Color.Black,
            ForeColor = Color.Gainsboro,
        };
        root.Controls.Add(_logBox, 0, 3);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _closeButton = new Button { Text = "Cerrar", AutoSize = true, Enabled = false };
        _closeButton.Click += (_, _) => Close();
        _cancelButton = new Button { Text = "Detener", AutoSize = true };
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        buttonsPanel.Controls.Add(_closeButton);
        buttonsPanel.Controls.Add(_cancelButton);
        root.Controls.Add(buttonsPanel, 0, 4);

        _installService.Log += (_, e) => AppendLog(e.Message);

        Load += async (_, _) => await RunAsync();
        FormClosing += OnFormClosing;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_installing)
        {
            return;
        }

        var choice = MessageBox.Show(this,
            "Hay una instalación en curso. ¿Detenerla y cerrar? Los instaladores ya lanzados podrían seguir ejecutándose.",
            "Instalación en curso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (choice != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _cts?.Cancel();
    }

    private async Task RunAsync()
    {
        _installing = true;
        _cts = new CancellationTokenSource();
        var rowsById = _rows.ToDictionary(r => r.Entry.Id);

        var progress = new Progress<InstallProgress>(p =>
        {
            if (rowsById.TryGetValue(p.Current.Id, out var row))
            {
                row.Status = p.Result is null ? "Instalando..." : DescribeOutcome(p.Result.Outcome);
            }

            _progressBar.Value = Math.Min(p.Completed, _progressBar.Maximum);
            _progressLabel.Text = $"{p.Completed} / {p.Total}";
        });

        try
        {
            Results = await _installService.InstallBatchAsync(_folder, _entries, _stopOnError, progress, _cts.Token);

            var succeeded = Results.Count(r => r.Outcome is InstallOutcome.Success or InstallOutcome.SuccessRebootRequired);
            var rebootNeeded = Results.Any(r => r.Outcome == InstallOutcome.SuccessRebootRequired);
            AppendLog($"--- Terminado: {succeeded}/{Results.Count} correctos.{(rebootNeeded ? " Algunos requieren reiniciar." : "")} ---");
            _progressLabel.Text = $"Terminado: {succeeded}/{Results.Count} correctos.";
        }
        catch (OperationCanceledException)
        {
            AppendLog("--- Instalación cancelada por el usuario. ---");
            _progressLabel.Text = "Cancelado.";
        }
        finally
        {
            _installing = false;
            _cancelButton.Enabled = false;
            _closeButton.Enabled = true;
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
