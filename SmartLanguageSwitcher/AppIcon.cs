using System.Drawing;
namespace SmartLanguageSwitcher;
internal static class AppIcon
{
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("SmartLanguageSwitcher.Assets.app.ico")
            ?? throw new InvalidOperationException("Application icon is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
