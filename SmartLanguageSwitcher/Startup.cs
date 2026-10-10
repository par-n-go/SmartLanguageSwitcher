using Microsoft.Win32;
namespace SmartLanguageSwitcher;
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "SmartLanguageSwitcher";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) is string command && command == Command();
    }
    private static string Command() => "\"" + Environment.ProcessPath + "\"";
    public static void Set(bool enabled)
    {
        if (enabled && string.Equals(Path.GetFileName(Environment.ProcessPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Publish the application and place its EXE in a permanent folder first.");
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(Name, Command()); else key.DeleteValue(Name, false);
    }
}
