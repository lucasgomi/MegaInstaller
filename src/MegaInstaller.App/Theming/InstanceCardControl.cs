using System.Drawing.Drawing2D;
using MegaInstaller.Core.Models;

namespace MegaInstaller.App.Theming;

/// <summary>
/// One instance "tile" in the Modern home screen's card gallery (used
/// instead of the classic DataGridView row). A special add-tile instance
/// (see <see cref="CreateAddTile"/>) has no <see cref="InstanceId"/> and
/// just invites creating a new one. Every other card carries its
/// instance's own accent color (or the theme's default accent, if none was
/// chosen) through its icon badge, count pill, and selection/hover border.
/// </summary>
public sealed class InstanceCardControl : Control
{
    public const int CardWidth = 220;
    public const int CardHeight = 150;

    public string? InstanceId { get; }

    private readonly string _name;
    private readonly string _description;
    private readonly Image? _icon;
    private readonly bool _isCustomIcon;
    private readonly int _programCount;
    private readonly Color _accentColor;
    private readonly bool _isAddTile;

    private bool _hovered;
    private bool _selected;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    private InstanceCardControl(string? instanceId, string name, string description, Image? icon, bool isCustomIcon, int programCount, Color accentColor, bool isAddTile)
    {
        InstanceId = instanceId;
        _name = name;
        _description = description;
        _icon = icon;
        _isCustomIcon = isCustomIcon;
        _programCount = programCount;
        _accentColor = accentColor;
        _isAddTile = isAddTile;

        Size = new Size(CardWidth, CardHeight);
        Margin = new Padding(8);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public static InstanceCardControl ForInstance(InstanceDefinition instance, int programCount, string folder) => new(
        instance.Id,
        instance.Name,
        instance.Description,
        InstanceIconCatalog.LoadForInstance(instance.IconKey, folder),
        InstanceIconCatalog.IsCustomKey(instance.IconKey),
        programCount,
        InstanceColorPalette.Resolve(instance.ColorHex, ModernPalette.Accent),
        isAddTile: false);

    public static InstanceCardControl CreateAddTile() =>
        new(null, "Nueva instancia", string.Empty, null, false, 0, ModernPalette.Accent, isAddTile: true);

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    // Painting is fully custom (see OnPaint); skipping the default
    // background fill avoids the same black-corner artifact ModernButton
    // had (a plain Control paints a "transparent" BackColor as solid black
    // once ControlStyles.OptimizedDoubleBuffer is on, instead of showing
    // the parent through) - here it showed up as corners outside the
    // rounded card looking like an extra, inconsistent border.
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? SystemColors.Control);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Inset by half the pen width: a stroke is centred on its path, so
        // a path along the very edge spills half of itself outside the
        // control, where it gets clipped - which is what made the border
        // look thicker on some sides than others.
        var borderWidth = _selected ? 2f : 1f;
        using var path = RoundedRect(new RectangleF(borderWidth / 2f, borderWidth / 2f, Width - borderWidth, Height - borderWidth), 12f);

        var borderColor = _selected ? _accentColor : _hovered ? ControlPaint.Light(_accentColor, 0.5f) : ModernPalette.Border;
        var backColor = _isAddTile ? ModernPalette.Background : ModernPalette.Surface;

        using (var backBrush = new SolidBrush(backColor))
        {
            g.FillPath(backBrush, path);
        }

        using (var pen = new Pen(borderColor, borderWidth))
        {
            if (_isAddTile)
            {
                pen.DashStyle = DashStyle.Dash;
            }

            g.DrawPath(pen, path);
        }

        if (_isAddTile)
        {
            PaintAddTile(g);
            return;
        }

        const int pad = 14;
        if (_icon is not null)
        {
            var iconBadgeRect = new Rectangle(pad - 4, pad - 4, 40, 40);
            using (var badgeBrush = new SolidBrush(InstanceColorPalette.Tint(_accentColor)))
            {
                g.FillEllipse(badgeBrush, iconBadgeRect);
            }

            using var clipPath = new GraphicsPath();
            clipPath.AddEllipse(iconBadgeRect);
            var previousClip = g.Clip;
            g.SetClip(clipPath, CombineMode.Replace);

            if (_isCustomIcon)
            {
                // A custom upload is already pre-cropped to a square for
                // exactly this (see ImageCropForm), so it can cover the
                // whole badge edge to edge like any circular avatar photo.
                g.DrawImage(_icon, iconBadgeRect);
            }
            else
            {
                // Built-in glyphs are drawn close to their full square
                // canvas, so covering the badge edge to edge clipped them
                // against the circle wherever their ink reached a corner
                // (e.g. a folder icon's tab). Drawing them smaller and
                // centred keeps the whole glyph inside the circle instead.
                const int glyphSize = 24;
                var glyphRect = new Rectangle(
                    iconBadgeRect.X + (iconBadgeRect.Width - glyphSize) / 2,
                    iconBadgeRect.Y + (iconBadgeRect.Height - glyphSize) / 2,
                    glyphSize, glyphSize);
                g.DrawImage(_icon, glyphRect);
            }

            g.Clip = previousClip;
        }

        using var nameFont = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
        var nameRect = new RectangleF(pad, pad + 40, Width - pad * 2, 20);
        using (var nameBrush = new SolidBrush(ModernPalette.TextPrimary))
        {
            g.DrawString(_name, nameFont, nameBrush, nameRect,
                new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
        }

        using var descFont = new Font(Font.FontFamily, 8.5F);
        var descRect = new RectangleF(pad, pad + 62, Width - pad * 2, 40);
        using (var descBrush = new SolidBrush(ModernPalette.TextSecondary))
        {
            g.DrawString(_description, descFont, descBrush, descRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
        }

        var badgeText = _programCount == 1 ? "1 programa" : $"{_programCount} programas";
        using var badgeFont = new Font(Font.FontFamily, 8F);
        var badgeSize = g.MeasureString(badgeText, badgeFont);
        var badgeRect = new RectangleF(pad, Height - pad - 20, badgeSize.Width + 14, 20);
        using var badgePath = RoundedRect(badgeRect, 10f);
        using (var badgeBrush = new SolidBrush(InstanceColorPalette.Tint(_accentColor)))
        {
            g.FillPath(badgeBrush, badgePath);
        }

        using (var badgeTextBrush = new SolidBrush(_accentColor))
        {
            g.DrawString(badgeText, badgeFont, badgeTextBrush, badgeRect.X + 7, badgeRect.Y + 3);
        }
    }

    private void PaintAddTile(Graphics g)
    {
        using var plusFont = new Font(Font.FontFamily, 22F, FontStyle.Bold);
        using var textBrush = new SolidBrush(ModernPalette.TextSecondary);
        var plusSize = g.MeasureString("+", plusFont);
        g.DrawString("+", plusFont, textBrush, (Width - plusSize.Width) / 2, 34);

        using var captionFont = new Font(Font.FontFamily, 9F);
        var capSize = g.MeasureString(_name, captionFont);
        g.DrawString(_name, captionFont, textBrush, (Width - capSize.Width) / 2, 82);
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
