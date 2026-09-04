namespace MegaInstaller.App.Theming;

/// <summary>
/// A Panel that paints without flicker. Panel.DoubleBuffered is protected,
/// so anything that repaints continuously (dragging the crop square, say)
/// needs a subclass to turn it on - otherwise every Invalidate erases the
/// panel before repainting it and the content visibly flashes.
/// </summary>
public sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }
}
