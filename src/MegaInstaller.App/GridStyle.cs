namespace MegaInstaller.App;

/// <summary>Shared, modern-ish DataGridView look applied consistently across the app's grids.</summary>
public static class GridStyle
{
    public static void Apply(DataGridView grid)
    {
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(230, 230, 230);

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeight = 30;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 247);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(50, 55, 60);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font.FontFamily, grid.Font.Size, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 228, 247);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 251);
    }
}
