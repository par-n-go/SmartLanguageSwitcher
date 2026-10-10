using System.Diagnostics;
using System.Text.Json;
namespace SmartLanguageSwitcher;
internal sealed record UpdatePlan(string Target, string Staged, string Hash, string Tag, int ParentId, long ParentStart, string Directory)
{
    public string Backup => Staged + ".bak";
    public string Manifest => Path.Combine(Directory, "plan.json");
    public string Ready => Path.Combine(Directory, "ready");
    public string Restarted => Path.Combine(Directory, "restarted");
}
internal static class UpdateInstaller
{
    internal static string Cache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartLanguageSwitcher", "updates");
    internal static string FailedVersionPath => Path.Combine(StateDirectory, "failed-version.txt");
    private static string StateDirectory => UpdateTests.IsTestProcess ? UpdateTests.TestRoot! : Cache;
    public static UpdatePlan Prepare(string target, string download, string hash, string tag, string directory)
    {
        target = Path.GetFullPath(target); directory = Path.GetFullPath(directory);
        if (!AutoUpdater.VerifyHash(download, hash)) throw new InvalidDataException("Update checksum failed.");
        string staged = target + ".update-" + Guid.NewGuid().ToString("N") + ".tmp";
        using var parent = Process.GetCurrentProcess();
        var plan = new UpdatePlan(target, staged, hash, tag, parent.Id, parent.StartTime.ToUniversalTime().Ticks, directory);
        try
        {
            File.Copy(download, staged, false);
            File.Copy(Environment.ProcessPath!, Path.Combine(directory, "helper.exe"), false);
            File.WriteAllText(plan.Manifest, JsonSerializer.Serialize(plan));
            return plan;
        }
        catch { TryDelete(staged); throw; }
    }
    internal static Process Start(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new IOException("Unable to start update process.");
    }
    public static async Task LaunchAsync(UpdatePlan plan, CancellationToken token, Func<bool>? canContinue = null)
    {
        using var helper = Start(Path.Combine(plan.Directory, "helper.exe"), "--apply-update", plan.Manifest);
        bool ready = false;
        try
        {
            for (int i = 0; i < 100; i++)
            {
                if (File.Exists(plan.Ready)) { token.ThrowIfCancellationRequested(); if (canContinue != null && !canContinue()) throw new OperationCanceledException(); ready = true; return; }
                if (helper.HasExited) throw new IOException("Update helper could not start.");
                await Task.Delay(100, token);
            }
            throw new IOException("Update helper timed out.");
        }
        finally { if (!ready && !helper.HasExited) { helper.Kill(); await helper.WaitForExitAsync(); } }
    }
    internal static void Commit(string staged, string target, string backup, string hash)
    {
        if (!AutoUpdater.VerifyHash(staged, hash)) throw new InvalidDataException("Staged update checksum failed.");
        File.Replace(staged, target, backup);
    }
    public static int Apply(string manifest)
    {
        UpdatePlan? plan = null; bool replaced = false, parentExited = false; Process? restarted = null;
        try
        {
            plan = JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid update plan.");
            if (Path.GetFullPath(manifest) != plan.Manifest || !IsCacheDirectory(plan.Directory) ||
                Path.GetDirectoryName(plan.Target) != Path.GetDirectoryName(plan.Staged) ||
                !plan.Staged.StartsWith(plan.Target + ".update-", StringComparison.OrdinalIgnoreCase) ||
                !plan.Staged.EndsWith(".tmp", StringComparison.Ordinal) || !plan.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid update paths.");
            using var parent = Process.GetProcessById(plan.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStart ||
                !string.Equals(parent.MainModule?.FileName, plan.Target, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update parent does not match target.");
            if (!AutoUpdater.VerifyHash(plan.Staged, plan.Hash)) throw new InvalidDataException("Staged checksum failed.");
            File.WriteAllText(plan.Ready, "ready");
            if (!parent.WaitForExit(30000)) throw new IOException("Application did not exit; update cancelled.");
            parentExited = true;
            for (int i = 0; ; i++)
            {
                try { Commit(plan.Staged, plan.Target, plan.Backup, plan.Hash); replaced = true; break; }
                catch (IOException) when (i < 20) { Thread.Sleep(250); }
            }
            restarted = Start(plan.Target, "--update-ready", plan.Restarted);
            for (int i = 0; i < 150 && !File.Exists(plan.Restarted); i++)
            {
                if (restarted.HasExited) break;
                Thread.Sleep(100);
            }
            if (!File.Exists(plan.Restarted)) throw new IOException("Updated application did not start.");
            TryDelete(plan.Backup); TryDelete(FailedVersionPath); TryDelete(Path.Combine(StateDirectory, "last-error.txt"));
            if (UpdateTests.IsTestProcess) File.WriteAllText(Path.Combine(plan.Directory, "test-completed"), "success");
            CleanDirectory(plan.Directory);
            return 0;
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            if (plan != null && replaced)
            {
                try
                {
                    if (restarted != null && !restarted.HasExited) { restarted.Kill(); restarted.WaitForExit(5000); }
                    File.Replace(plan.Backup, plan.Target, null);
                    File.WriteAllText(FailedVersionPath, plan.Tag);
                    using var old = Start(plan.Target);
                    if (UpdateTests.IsTestProcess) File.WriteAllText(Path.Combine(plan.Directory, "test-completed"), "rollback");
                }
                catch (Exception rollback) { WriteError(e.Message + " Rollback: " + rollback.Message); }
            }
            else if (plan != null && parentExited)
            {
                try { using var old = Start(plan.Target); } catch { }
            }
            return 1;
        }
        finally { restarted?.Dispose(); }
    }
    internal static bool IsCacheDirectory(string directory) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(directory)), Cache, StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(Path.GetFileName(directory), "N", out _);
    public static void SignalReady(string[] args)
    {
        if (args.Length != 2 || args[0] != "--update-ready") return;
        string path = Path.GetFullPath(args[1]);
        if (Path.GetFileName(path) != "restarted" || !IsCacheDirectory(Path.GetDirectoryName(path)!)) return;
        File.WriteAllText(path, "ready");
    }
    internal static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    internal static void WriteError(string message)
    {
        try { Directory.CreateDirectory(StateDirectory); File.WriteAllText(Path.Combine(StateDirectory, "last-error.txt"), message); } catch { }
    }
    internal static void CleanDirectory(string directory)
    {
        if (!IsCacheDirectory(directory)) return;
        foreach (var name in new[] { "download.exe", "helper.exe", "plan.json", "ready", "restarted" }) TryDelete(Path.Combine(directory, name));
        try { System.IO.Directory.Delete(directory, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    internal static void CleanOldFiles()
    {
        try
        {
            if (!Directory.Exists(Cache)) return;
            foreach (var directory in Directory.EnumerateDirectories(Cache))
                if (IsCacheDirectory(directory) && Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-1)) CleanDirectory(directory);
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}


