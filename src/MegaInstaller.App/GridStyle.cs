namespace MegaInstaller.App;

/// <summary>Shared, modern-ish DataGridView look applied consistently across the app's grids.</summary>
public static class GridStyle
{
    /// <summary>
    /// A blank 1x1 transparent placeholder for image columns. DataGridViewImageCell
    /// throws when a bound value is null and no fallback is set on the column's
    /// DefaultCellStyle.NullValue - which is exactly what a row with no icon looks
    /// like (an instance with no IconKey, or a failed icon extraction). The grid
    /// swallows that per-cell error but can lose track of which cell is "current",
    /// which is why clicking an icon looked like it was selecting an unrelated cell.
    /// </summary>
    public static readonly Image BlankIcon = new Bitmap(1, 1);

    public static void ApplyIconColumn(DataGridViewImageColumn column)
    {
        column.DefaultCellStyle.NullValue = BlankIcon;
        // Binding an Image-typed POCO property by reflection (DataPropertyName)
        // can leave the cell's inferred ValueType mismatched from what the
        // property actually returns; being explicit avoids a formatting
        // exception on every paint, not just null-valued cells.
        column.ValueType = typeof(Image);
        column.ReadOnly = true;
        column.SortMode = DataGridViewColumnSortMode.NotSortable;
    }

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
