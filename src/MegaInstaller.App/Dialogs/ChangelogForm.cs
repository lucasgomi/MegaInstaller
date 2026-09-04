using MegaInstaller.App.Theming;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Dialogs;

/// <summary>Shows a fetched GitHub release's notes in-app, with a link out to the full release page.</summary>
public sealed class ChangelogForm : Form
{
    public ChangelogForm(GitHubReleaseInfo release)
    {
        Text = $"Novedades - {release.TagName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 420);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = $"{release.TagName} - publicado el {release.PublishedAt:dd/MM/yyyy}",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);

        var notesBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = string.IsNullOrWhiteSpace(release.Body) ? "(Esta versión no tiene notas.)" : MarkdownLite.ToPlainText(release.Body),
        };
        root.Controls.Add(notesBox, 0, 1);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = AppTheme.CreateButton("Cerrar");
        closeButton.DialogResult = DialogResult.OK;
        var openButton = AppTheme.CreateButton("Ver en GitHub", primary: true);
        openButton.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = release.HtmlUrl, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
            {
                MessageBox.Show(this, $"No se pudo abrir el enlace: {ex.Message}", "No se pudo abrir",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttonsPanel.Controls.Add(closeButton);
        buttonsPanel.Controls.Add(openButton);
        root.Controls.Add(buttonsPanel, 0, 2);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        AppTheme.StyleForm(this);
    }
}
