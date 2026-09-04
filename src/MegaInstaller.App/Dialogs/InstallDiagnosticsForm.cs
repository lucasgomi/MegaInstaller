using MegaInstaller.App.Theming;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// The troubleshooter (enabled from Ajustes): for each program that failed
/// in the batch it explains what the exit code means for that installer
/// family, offers argument sets worth retrying, and can either try one right
/// away or save it back into the program so the next run uses it.
/// </summary>
public sealed class InstallDiagnosticsForm : Form
{
    private readonly string _folder;
    private readonly List<(InstallerEntry Entry, InstallResult Result, InstallDiagnosis Diagnosis)> _failures;
    private readonly IReadOnlyDictionary<string, string>? _resolvedPaths;
    private readonly ManifestService _manifestService = new();
    private readonly InstallService _installService = new();

    private readonly ListBox _failureList;
    private readonly Label _summaryLabel;
    private readonly Label _adviceLabel;
    private readonly ComboBox _argumentsCombo;
    private readonly Label _statusLabel;
    private readonly Button _retryButton;
    private readonly Button _saveButton;

    public bool ManifestChanged { get; private set; }

    public InstallDiagnosticsForm(string folder, IEnumerable<(InstallerEntry Entry, InstallResult Result)> failures, IReadOnlyDictionary<string, string>? resolvedPaths = null)
    {
        _folder = folder;
        _resolvedPaths = resolvedPaths;
        _failures = failures
            .Select(f => (f.Entry, f.Result, InstallDiagnostics.Analyze(f.Entry, f.Result)))
            .ToList();

        Text = "Diagnóstico de instalación";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 420);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        _failureList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var failure in _failures)
        {
            _failureList.Items.Add(failure.Entry.Name);
        }
        _failureList.SelectedIndexChanged += (_, _) => ShowSelected();
        root.Controls.Add(_failureList, 0, 0);

        var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12, 0, 0, 0) };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(detailLayout, 1, 0);

        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font(Font, FontStyle.Bold),
        };
        detailLayout.Controls.Add(_summaryLabel, 0, 0);

        _adviceLabel = new Label { Dock = DockStyle.Fill, AutoSize = false };
        detailLayout.Controls.Add(_adviceLabel, 0, 1);

        detailLayout.Controls.Add(new Label { Text = "Argumentos a probar:", AutoSize = true }, 0, 2);

        _argumentsCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        detailLayout.Controls.Add(_argumentsCombo, 0, 3);

        _statusLabel = new Label { Dock = DockStyle.Fill, AutoSize = false, ForeColor = SystemColors.GrayText };
        detailLayout.Controls.Add(_statusLabel, 0, 4);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = AppTheme.CreateButton("Cerrar");
        closeButton.DialogResult = DialogResult.OK;
        _retryButton = AppTheme.CreateButton("Probar ahora", primary: true);
        _retryButton.Click += OnRetry;
        _saveButton = AppTheme.CreateButton("Guardar en el programa");
        _saveButton.Click += OnSaveArguments;
        buttonsPanel.Controls.Add(closeButton);
        buttonsPanel.Controls.Add(_retryButton);
        buttonsPanel.Controls.Add(_saveButton);
        root.Controls.Add(buttonsPanel, 0, 1);
        root.SetColumnSpan(buttonsPanel, 2);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        if (_failures.Count > 0)
        {
            _failureList.SelectedIndex = 0;
        }

        AppTheme.StyleForm(this);
    }

    private (InstallerEntry Entry, InstallResult Result, InstallDiagnosis Diagnosis)? Selected =>
        _failureList.SelectedIndex >= 0 && _failureList.SelectedIndex < _failures.Count
            ? _failures[_failureList.SelectedIndex]
            : null;

    private void ShowSelected()
    {
        _statusLabel.Text = string.Empty;
        if (Selected is not { } selected)
        {
            return;
        }

        var exitCodeText = selected.Result.ExitCode is { } code ? $" (código {code})" : string.Empty;
        _summaryLabel.Text = selected.Diagnosis.Summary + exitCodeText;
        _adviceLabel.Text = selected.Diagnosis.Advice;

        _argumentsCombo.Items.Clear();
        foreach (var candidate in selected.Diagnosis.SuggestedArguments)
        {
            _argumentsCombo.Items.Add(candidate);
        }

        _argumentsCombo.Text = selected.Diagnosis.SuggestedArguments.Count > 0
            ? selected.Diagnosis.SuggestedArguments[0]
            : selected.Entry.Arguments;

        var canRetry = !string.IsNullOrWhiteSpace(_argumentsCombo.Text) || selected.Diagnosis.SuggestedArguments.Count > 0;
        _retryButton.Enabled = canRetry;
        _saveButton.Enabled = canRetry;
    }

    private async void OnRetry(object? sender, EventArgs e)
    {
        if (Selected is not { } selected)
        {
            return;
        }

        // Runs a throwaway copy so a failed experiment never changes what is
        // stored for the program - only "Guardar en el programa" does that.
        var trial = CloneWithArguments(selected.Entry, _argumentsCombo.Text.Trim());

        _retryButton.Enabled = false;
        _saveButton.Enabled = false;
        _statusLabel.Text = "Probando...";
        try
        {
            var result = await _installService.InstallOneAsync(_folder, trial, CancellationToken.None, _resolvedPaths);
            _statusLabel.Text = result.Outcome switch
            {
                InstallOutcome.Success => "Funcionó. Pulsa \"Guardar en el programa\" para dejarlo así.",
                InstallOutcome.SuccessRebootRequired => "Funcionó, pero pide reiniciar. Puedes guardar estos argumentos.",
                _ => $"Sigue fallando: {result.Message ?? result.Outcome.ToString()}",
            };
        }
        finally
        {
            _retryButton.Enabled = true;
            _saveButton.Enabled = true;
        }
    }

    private void OnSaveArguments(object? sender, EventArgs e)
    {
        if (Selected is not { } selected)
        {
            return;
        }

        var arguments = _argumentsCombo.Text.Trim();
        try
        {
            var manifest = _manifestService.Load(_folder);
            var stored = manifest.Items.FirstOrDefault(i => i.Id == selected.Entry.Id);
            if (stored is null)
            {
                _statusLabel.Text = "Ese programa ya no está en la lista de la carpeta.";
                return;
            }

            stored.Arguments = arguments;
            _manifestService.Save(_folder, manifest);
            selected.Entry.Arguments = arguments;
            ManifestChanged = true;
            _statusLabel.Text = "Guardado. La próxima instalación usará estos argumentos.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _statusLabel.Text = $"No se pudo guardar: {ex.Message}";
        }
    }

    private static InstallerEntry CloneWithArguments(InstallerEntry entry, string arguments) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        FileName = entry.FileName,
        MirrorUrl = entry.MirrorUrl,
        ExpectedSha256 = entry.ExpectedSha256,
        Type = entry.Type,
        Arguments = arguments,
        TargetInstallDir = entry.TargetInstallDir,
        RunAsAdmin = entry.RunAsAdmin,
        Order = entry.Order,
    };
}
