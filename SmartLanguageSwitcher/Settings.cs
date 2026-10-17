using System.Text.Json;
namespace SmartLanguageSwitcher;
internal enum ShortcutKind { AltShift, WinSpace, CtrlShift }
internal sealed class Settings
{
    public ShortcutKind Shortcut { get; set; } = ShortcutKind.AltShift;
    public bool AutoUpdate { get; set; } = true;
    public bool ExcludeBritishEnglish { get; set; }
    internal bool AllowsLayout(nint layout) => !ExcludeBritishEnglish || (layout.ToInt64() & 0xffff) != 0x0809;
    public bool StartWithWindows { get; set; }
    public string? LastLayout { get; set; }
    public string? PreviousLayout { get; set; }
    internal static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartLanguageSwitcher", "settings.json");
    public static Settings Load(string? path = null)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path ?? PathName)) ?? new();
            if (!Enum.IsDefined(value.Shortcut)) value.Shortcut = ShortcutKind.AltShift;
            return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public bool Save(string? path = null)
    {
        path ??= PathName;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
