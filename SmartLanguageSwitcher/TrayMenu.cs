using System.Drawing;
using System.Drawing.Drawing2D;
namespace SmartLanguageSwitcher;
internal static class TrayMenu
{
    public static ContextMenuStrip Create(Action settings, Action<bool> pauseChanged, Action exit, Action? checkUpdates = null)
    {
        var menu = new ContextMenuStrip { BackColor = Theme.Background, ForeColor = Color.White,
            Font = new Font("Segoe UI", 10), Renderer = new MenuRenderer(), ShowImageMargin = false,
            ShowCheckMargin = true, Padding = new Padding(5), AutoSize = true };
        menu.Items.Add("Settings", null, (_, _) => settings());
        var pause = new ToolStripMenuItem("Pause") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => pauseChanged(pause.Checked);
        menu.Items.Add(pause); menu.Items.Add(new ToolStripSeparator());
        if (checkUpdates != null) menu.Items.Add("Check for updates", null, (_, _) => checkUpdates());
        menu.Items.Add("Exit", null, (_, _) => exit());
        foreach (ToolStripItem item in menu.Items)
            if (item is ToolStripMenuItem) { item.Padding = new Padding(8, 7, 22, 7); item.ForeColor = Color.White; }
        return menu;
    }
    private sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() => RoundedEdges = false;
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Background);
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Surface); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Theme.Rounded(new RectangleF(1, 1, e.Item.Width - 2, e.Item.Height - 2), 6 * (e.ToolStrip?.DeviceDpi ?? 96) / 96f);
            using var brush = new SolidBrush(Theme.Surface); e.Graphics.FillPath(brush, shape);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.White : Theme.Muted; base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            using var pen = new Pen(Theme.Accent, Math.Max(1.5f, 2 * (e.ToolStrip?.DeviceDpi ?? 96) / 96f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            e.Graphics.DrawLines(pen, new PointF[] { new(r.Left + r.Width * .18f, r.Top + r.Height * .5f), new(r.Left + r.Width * .42f, r.Top + r.Height * .74f), new(r.Left + r.Width * .84f, r.Top + r.Height * .25f) });
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Surface); e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }
    }
}
