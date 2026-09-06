using System.Drawing.Drawing2D;

namespace Gradual.Controls;

public class RoundedPanel : Panel
{
    public int Radius { get; set; } = 18;
    public Color BorderColor { get; set; } = Color.FromArgb(223, 229, 238);

    public RoundedPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        
        // Get the client rectangle, accounting for scrollbars
        var rect = ClientRectangle;
        
        using var path = CreateRoundedRectanglePath(rect, Radius);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);

        using var borderPen = new Pen(BorderColor, 1.2f);
        e.Graphics.DrawPath(borderPen, path);

        base.OnPaint(e);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var x = rect.X;
        var y = rect.Y;
        var width = rect.Width;
        var height = rect.Height;
        var r = Math.Max(1, radius);

        path.AddArc(x, y, r, r, 180, 90);
        path.AddArc(x + width - r, y, r, r, 270, 90);
        path.AddArc(x + width - r, y + height - r, r, r, 0, 90);
        path.AddArc(x, y + height - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}
