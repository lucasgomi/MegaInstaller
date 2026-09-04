using System.Drawing.Drawing2D;

namespace MegaInstaller.App.Theming;

/// <summary>A flat, rounded, owner-drawn button for the Modern theme (see <see cref="AppTheme.CreateButton"/>).</summary>
public sealed class ModernButton : Button
{
    public bool Primary { get; set; }

    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Padding = new Padding(10, 4, 10, 4);
        MinimumSize = new Size(0, 30);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }

    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(rect, 6);

        Color fill;
        Color textColor;
        Color? borderColor;

        if (!Enabled)
        {
            fill = Primary ? Color.FromArgb(0xD8, 0xD9, 0xF7) : ModernPalette.Surface;
            textColor = Color.FromArgb(0xAF, 0xB3, 0xBD);
            borderColor = Primary ? null : ModernPalette.Border;
        }
        else if (Primary)
        {
            fill = _pressed ? ModernPalette.AccentPressed : _hovered ? ModernPalette.AccentHover : ModernPalette.Accent;
            textColor = ModernPalette.OnAccent;
            borderColor = null;
        }
        else
        {
            fill = _pressed ? ModernPalette.AccentSoft : _hovered ? Color.FromArgb(0xF3, 0xF3, 0xFC) : ModernPalette.Surface;
            textColor = ModernPalette.TextPrimary;
            borderColor = ModernPalette.Border;
        }

        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        if (borderColor is { } bc)
        {
            using var pen = new Pen(bc);
            g.DrawPath(pen, path);
        }

        if (Focused && Enabled)
        {
            using var focusPen = new Pen(ModernPalette.Accent, 1.5f) { DashStyle = DashStyle.Dot };
            using var focusPath = RoundedRect(Rectangle.Inflate(rect, -2, -2), 5);
            g.DrawPath(focusPen, focusPath);
        }

        TextRenderer.DrawText(g, Text, Font, rect, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
