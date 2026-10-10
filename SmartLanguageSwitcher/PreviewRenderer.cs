using System.Drawing;
using System.Drawing.Imaging;
namespace SmartLanguageSwitcher;
internal static class PreviewRenderer
{
    public static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        using var form = new SettingsForm(new Settings { Shortcut = ShortcutKind.WinSpace }, preview: true, render: true);
        form.Opacity = 0; form.ShowInTaskbar = false; form.Show();
        form.PerformAutoScale(); form.PerformLayout();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var origin = form.PointToScreen(Point.Empty);
        using var client = bitmap.Clone(new Rectangle(origin.X - form.Left, origin.Y - form.Top, form.ClientSize.Width, form.ClientSize.Height), bitmap.PixelFormat);
        client.Save(Path.Combine(directory, "settings.png"), ImageFormat.Png);
        form.Close();
        using var menu = TrayMenu.Create(() => { }, _ => { }, () => { });
        ((ToolStripMenuItem)menu.Items[1]).Checked = true;
        menu.Items[0].Select();
        menu.Size = menu.GetPreferredSize(Size.Empty);
        _ = menu.Handle; menu.PerformLayout();
        using var menuBitmap = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(menuBitmap, new Rectangle(Point.Empty, menuBitmap.Size));
        menuBitmap.Save(Path.Combine(directory, "tray-menu.png"), ImageFormat.Png);
    }
}

