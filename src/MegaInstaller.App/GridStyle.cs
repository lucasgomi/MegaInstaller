using MegaInstaller.App.Theming;

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
        if (AppTheme.IsModern)
        {
            ApplyModern(grid);
        }
        else
        {
            ApplyClassic(grid);
        }
    }

    private static void ApplyClassic(DataGridView grid)
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
        // Without an explicit override, a header cell's "selected" state
        // (e.g. the header of whichever column the current cell sits in)
        // falls back to DataGridView.DefaultCellStyle.SelectionBackColor
        // below - this is what actually produced the "top of the column
        // highlights blue" glitch, not anything about the icon cells
        // themselves. Pinning both to the normal header colors makes a
        // header look the same regardless of selection state.
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = grid.ColumnHeadersDefaultCellStyle.ForeColor;

        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 228, 247);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 251);
    }

    private static void ApplyModern(DataGridView grid)
    {
        grid.BackgroundColor = ModernPalette.Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        grid.GridColor = ModernPalette.Surface;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ModernPalette.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ModernPalette.TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font.FontFamily, grid.Font.Size, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        // Same header-selection pin as Classic (see above) - still needed
        // here regardless of palette, since it's what stops the header from
        // ever taking on a data cell's selection color.
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = grid.ColumnHeadersDefaultCellStyle.ForeColor;

        grid.DefaultCellStyle.BackColor = ModernPalette.Surface;
        grid.DefaultCellStyle.ForeColor = ModernPalette.TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = ModernPalette.AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = ModernPalette.TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(8, 4, 4, 4);
        grid.AlternatingRowsDefaultCellStyle.BackColor = ModernPalette.Surface;
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 34);

        // A thin accent line under the whole header row instead of a hard
        // border - drawing it per-cell keeps it correct as columns resize.
        grid.CellPainting -= DrawModernHeaderUnderline;
        grid.CellPainting += DrawModernHeaderUnderline;
    }

    private static void DrawModernHeaderUnderline(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.Graphics is null) return;

        e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Border);
        using var pen = new Pen(ModernPalette.Accent, 2);
        e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
        e.Handled = true;
    }
}
