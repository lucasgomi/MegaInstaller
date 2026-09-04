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

    // Painting is fully custom (see OnPaint), so the default background fill
    // is skipped entirely - a plain Control combined with
    // ControlStyles.OptimizedDoubleBuffer renders a transparent BackColor as
    // solid black instead of actually showing the parent through, which is
    // what produced black-looking corners outside the rounded button shape.
    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(Parent?.BackColor ?? SystemColors.Control);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Inset by half the pen width: a stroke is centred on its path, so
        // a path along the very edge spills half of itself outside the
        // control, where it gets clipped and reads as an uneven border.
        const float borderWidth = 1f;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(new RectangleF(borderWidth / 2f, borderWidth / 2f, Width - borderWidth, Height - borderWidth), 6f);

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
            using var pen = new Pen(bc, borderWidth);
            g.DrawPath(pen, path);
        }

        if (Focused && Enabled)
        {
            using var focusPen = new Pen(ModernPalette.Accent, 1.5f) { DashStyle = DashStyle.Dot };
            using var focusPath = RoundedRect(new RectangleF(3f, 3f, Width - 6f, Height - 6f), 5f);
            g.DrawPath(focusPen, focusPath);
        }

        if (Image is not null)
        {
            // Laid out from the same Padding that AutoSize measured with, so
            // the drawn content matches the width the button was given.
            var iconSize = AppTheme.ButtonIconSize;
            var iconRect = new Rectangle(rect.X + Padding.Left, rect.Y + (rect.Height - iconSize) / 2, iconSize, iconSize);
            g.DrawImage(Image, iconRect);

            var textRect = new Rectangle(iconRect.Right + 6, rect.Y, rect.Width - (iconRect.Right + 6 - rect.X) - Padding.Right, rect.Height);
            TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        else
        {
            TextRenderer.DrawText(g, Text, Font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var diameter = Math.Max(1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
