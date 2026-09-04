using System.Drawing.Drawing2D;
using MegaInstaller.App.Theming;

namespace MegaInstaller.App.Dialogs;

/// <summary>
/// Lets the user pick a square crop out of an arbitrary image before it
/// becomes a custom instance icon. The crop square is always the largest
/// one that fits the source image and can be dragged (not resized) into
/// place - a fixed size keeps the interaction to a single, easy-to-get-right
/// gesture instead of resize handles.
/// </summary>
public sealed class ImageCropForm : Form
{
    private const int PreviewSize = 380;

    private readonly Image _source;
    private readonly float _scale;
    private readonly RectangleF _imageBounds;
    private readonly Panel _previewPanel;

    private RectangleF _cropBounds;
    private bool _dragging;
    private Point _dragStart;
    private PointF _cropStartTopLeft;

    /// <summary>The cropped, square result once the user confirms. Caller owns disposing it.</summary>
    public Bitmap? Result { get; private set; }

    public ImageCropForm(Image source)
    {
        _source = source;

        var srcSize = source.Size;
        _scale = Math.Min((float)PreviewSize / srcSize.Width, (float)PreviewSize / srcSize.Height);
        var dispWidth = srcSize.Width * _scale;
        var dispHeight = srcSize.Height * _scale;
        var offsetX = (PreviewSize - dispWidth) / 2f;
        var offsetY = (PreviewSize - dispHeight) / 2f;
        _imageBounds = new RectangleF(offsetX, offsetY, dispWidth, dispHeight);

        var squareSize = Math.Min(dispWidth, dispHeight);
        _cropBounds = new RectangleF(
            offsetX + (dispWidth - squareSize) / 2f,
            offsetY + (dispHeight - squareSize) / 2f,
            squareSize,
            squareSize);

        Text = "Recortar icono";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(PreviewSize + 24, PreviewSize + 24 + 64);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, PreviewSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "Arrastra el recuadro para elegir la zona cuadrada.", AutoSize = true }, 0, 0);

        _previewPanel = new Panel { Width = PreviewSize, Height = PreviewSize, BorderStyle = BorderStyle.FixedSingle };
        _previewPanel.Paint += OnPreviewPaint;
        _previewPanel.MouseDown += OnPreviewMouseDown;
        _previewPanel.MouseMove += OnPreviewMouseMove;
        _previewPanel.MouseUp += (_, _) => _dragging = false;
        root.Controls.Add(_previewPanel, 0, 1);

        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = AppTheme.CreateButton("Cancelar");
        cancelButton.DialogResult = DialogResult.Cancel;
        var cropButton = AppTheme.CreateButton("Recortar", primary: true);
        cropButton.Click += OnCrop;
        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(cropButton);
        root.Controls.Add(buttonsPanel, 0, 2);

        AcceptButton = cropButton;
        CancelButton = cancelButton;

        AppTheme.StyleForm(this);
    }

    private void OnPreviewPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(_source, _imageBounds);

        using var overlayBrush = new SolidBrush(Color.FromArgb(140, Color.Black));
        using var dimmedOutside = new Region(new RectangleF(0, 0, PreviewSize, PreviewSize));
        dimmedOutside.Exclude(_cropBounds);
        g.FillRegion(overlayBrush, dimmedOutside);

        using var pen = new Pen(Color.White, 2f);
        g.DrawRectangle(pen, _cropBounds.X, _cropBounds.Y, _cropBounds.Width, _cropBounds.Height);
    }

    private void OnPreviewMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_cropBounds.Contains(e.Location))
        {
            return;
        }

        _dragging = true;
        _dragStart = e.Location;
        _cropStartTopLeft = _cropBounds.Location;
    }

    private void OnPreviewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var dx = e.X - _dragStart.X;
        var dy = e.Y - _dragStart.Y;
        var maxX = Math.Max(_imageBounds.X, _imageBounds.Right - _cropBounds.Width);
        var maxY = Math.Max(_imageBounds.Y, _imageBounds.Bottom - _cropBounds.Height);
        var newX = Math.Clamp(_cropStartTopLeft.X + dx, _imageBounds.X, maxX);
        var newY = Math.Clamp(_cropStartTopLeft.Y + dy, _imageBounds.Y, maxY);

        _cropBounds = new RectangleF(newX, newY, _cropBounds.Width, _cropBounds.Height);
        _previewPanel.Invalidate();
    }

    private void OnCrop(object? sender, EventArgs e)
    {
        var srcX = (_cropBounds.X - _imageBounds.X) / _scale;
        var srcY = (_cropBounds.Y - _imageBounds.Y) / _scale;
        var srcSize = _cropBounds.Width / _scale;

        const int outputSize = 256;
        var bitmap = new Bitmap(outputSize, outputSize);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImage(_source,
                new Rectangle(0, 0, outputSize, outputSize),
                new RectangleF(srcX, srcY, srcSize, srcSize),
                GraphicsUnit.Pixel);
        }

        Result = bitmap;
        DialogResult = DialogResult.OK;
        Close();
    }
}
