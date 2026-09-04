using System.Drawing.Drawing2D;
using MegaInstaller.Core.Models;

namespace MegaInstaller.App.Theming;

/// <summary>
/// One instance "tile" in the Modern home screen's card gallery (used
/// instead of the classic DataGridView row). A special add-tile instance
/// (see <see cref="CreateAddTile"/>) has no <see cref="InstanceId"/> and
/// just invites creating a new one.
/// </summary>
public sealed class InstanceCardControl : Control
{
    public const int CardWidth = 220;
    public const int CardHeight = 150;

    public string? InstanceId { get; }

    private readonly string _name;
    private readonly string _description;
    private readonly Image? _icon;
    private readonly int _programCount;
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

    private InstanceCardControl(string? instanceId, string name, string description, Image? icon, int programCount, bool isAddTile)
    {
        InstanceId = instanceId;
        _name = name;
        _description = description;
        _icon = icon;
        _programCount = programCount;
        _isAddTile = isAddTile;

        Size = new Size(CardWidth, CardHeight);
        Margin = new Padding(8);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public static InstanceCardControl ForInstance(InstanceDefinition instance, int programCount) =>
        new(instance.Id, instance.Name, instance.Description, InstanceIconCatalog.Load(instance.IconKey), programCount, isAddTile: false);

    public static InstanceCardControl CreateAddTile() =>
        new(null, "Nueva instancia", string.Empty, null, 0, isAddTile: true);

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(rect, 12);

        var borderColor = _selected ? ModernPalette.Accent : _hovered ? ModernPalette.AccentSoftBorder : ModernPalette.Border;
        var backColor = _isAddTile ? ModernPalette.Background : ModernPalette.Surface;

        using (var backBrush = new SolidBrush(backColor))
        {
            g.FillPath(backBrush, path);
        }

        using (var pen = new Pen(borderColor, _selected ? 2f : 1f))
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
            g.DrawImage(_icon, new Rectangle(pad, pad, 32, 32));
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
        using var badgePath = RoundedRect(Rectangle.Round(badgeRect), 10);
        using (var badgeBrush = new SolidBrush(ModernPalette.AccentSoft))
        {
            g.FillPath(badgeBrush, badgePath);
        }

        using (var badgeTextBrush = new SolidBrush(ModernPalette.Accent))
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
