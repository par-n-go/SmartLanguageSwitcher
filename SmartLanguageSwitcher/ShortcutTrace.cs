using System.Collections.Concurrent;
namespace SmartLanguageSwitcher;
internal static class ShortcutTrace
{
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartLanguageSwitcher", "diagnostics");
    private static readonly ConcurrentQueue<string> lines = new();
    private static DateTime expires;
    private static System.Threading.Timer? timer;
    private static int flushing;
    private static long nextRefresh;
    internal static bool Enabled => DateTime.UtcNow < expires;
    internal static void Initialize()
    {
        if (Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 30000;
        try
        {
            string flag = Path.Combine(DirectoryPath, "enabled-until.txt");
            expires = File.Exists(flag) ? ParseExpiry(File.ReadAllText(flag)) : default;
            if (!Enabled || timer != null) return;
            timer = new System.Threading.Timer(_ => Flush(), null, 1000, 1000);
            Write("start version=" + AutoUpdater.CurrentVersion.ToString(3));
        }
        catch (IOException) { expires = default; }
        catch (UnauthorizedAccessException) { expires = default; }
    }
    internal static DateTime ParseExpiry(string value) => DateTime.TryParseExact(value.Trim(), "O", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind, out var result) ? result.ToUniversalTime() : default;
    internal static void Write(string message)
    {
        if (!Enabled || lines.Count >= 1000) return;
        lines.Enqueue(DateTime.UtcNow.ToString("O") + " tick=" + Environment.TickCount64 + " " + message);
    }
    private static void Flush()
    {
        if (Interlocked.Exchange(ref flushing, 1) != 0) return;
        try
        {
            var batch = new List<string>();
            while (lines.TryDequeue(out var line)) batch.Add(line);
            if (batch.Count == 0) return;
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "shortcuts.log");
            if (File.Exists(path) && new FileInfo(path).Length > 128 * 1024)
                File.Move(path, path + ".previous", true);
            File.AppendAllLines(path, batch);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally { Volatile.Write(ref flushing, 0); }
    }
}
