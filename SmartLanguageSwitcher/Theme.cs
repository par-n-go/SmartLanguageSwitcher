using System.Drawing;
using System.Drawing.Drawing2D;
namespace SmartLanguageSwitcher;
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(18, 23, 36);
    public static readonly Color Surface = Color.FromArgb(29, 36, 53);
    public static readonly Color Muted = Color.FromArgb(161, 173, 195);
    public static readonly Color Accent = Color.FromArgb(97, 231, 212);
    public static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    public static void PaintControl(Control control, PaintEventArgs e, float radius, bool focus)
    {
        e.Graphics.Clear(control.Parent?.BackColor ?? Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Rounded(new RectangleF(.5f, .5f, control.Width - 1f, control.Height - 1f), radius * control.DeviceDpi / 96f);
        using var brush = new SolidBrush(control.BackColor);
        e.Graphics.FillPath(brush, shape);
        TextRenderer.DrawText(e.Graphics, control.Text, control.Font, control.ClientRectangle,
            control.Enabled ? control.ForeColor : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (focus)
        {
            using var pen = new Pen(Color.FromArgb(170, control.ForeColor), Math.Max(1, control.DeviceDpi / 96f)) { DashStyle = DashStyle.Dot };
            float inset = 4 * control.DeviceDpi / 96f;
            using var ring = Rounded(new RectangleF(inset, inset, control.Width - 2 * inset, control.Height - 2 * inset), Math.Max(2, radius - 3) * control.DeviceDpi / 96f);
            e.Graphics.DrawPath(pen, ring);
        }
    }
}
internal sealed class SmoothRadio : RadioButton
{
    public SmoothRadio() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaint(PaintEventArgs e) => Theme.PaintControl(this, e, 8, Focused && ShowFocusCues);
    protected override void OnPaintBackground(PaintEventArgs e) { }
}
internal sealed class SmoothToggle : CheckBox
{
    public SmoothToggle() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaint(PaintEventArgs e) => Theme.PaintControl(this, e, 18, Focused && ShowFocusCues);
    protected override void OnPaintBackground(PaintEventArgs e) { }
}
internal sealed class SmoothButton : Button
{
    public SmoothButton() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaint(PaintEventArgs e) => Theme.PaintControl(this, e, 10, Focused && ShowFocusCues);
    protected override void OnPaintBackground(PaintEventArgs e) { }
}
internal sealed class SmoothPanel : Panel
{
    public SmoothPanel() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1f, Height - 1f), 10 * DeviceDpi / 96f);
        using var brush = new SolidBrush(BackColor); e.Graphics.FillPath(brush, path);
    }
}
