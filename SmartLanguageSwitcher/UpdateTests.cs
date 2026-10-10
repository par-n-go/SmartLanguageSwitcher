using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace SmartLanguageSwitcher;
internal static class UpdateTests
{
    internal static string? TestRoot => Environment.GetEnvironmentVariable("SLS_UPDATE_TEST_ROOT");
    internal static bool IsTestProcess => TestRoot is { } root && UpdateInstaller.IsCacheDirectory(root) &&
        string.Equals(Path.GetDirectoryName(Environment.ProcessPath), root, StringComparison.OrdinalIgnoreCase);
    public static void Unit(Action<bool, string> check)
    {
        string Json(string tag = "v99.1.0", bool draft = false, bool prerelease = false, bool wrongHost = false) => JsonSerializer.Serialize(new {
            tag_name = tag, draft, prerelease, assets = new[] {
                new { name = "SmartLanguageSwitcher.exe", state = "uploaded", size = 5000, browser_download_url = (wrongHost ? "https://example.com" : AutoUpdater.Repository) + "/releases/download/" + tag + "/SmartLanguageSwitcher.exe" },
                new { name = "SHA256SUMS.txt", state = "uploaded", size = 93, browser_download_url = AutoUpdater.Repository + "/releases/download/" + tag + "/SHA256SUMS.txt" }
            }
        });
        check(AutoUpdater.ParseRelease(Json(), new Version(1, 0, 0, 0))?.Version == new Version(99, 1, 0, 0), "new stable release selected");
        check(AutoUpdater.ParseRelease(Json(draft: true), new Version(1, 0, 0, 0)) == null, "draft update excluded");
        check(AutoUpdater.ParseRelease(Json(prerelease: true), new Version(1, 0, 0, 0)) == null, "prerelease update excluded");
        check(AutoUpdater.ParseRelease(Json("v1.0.0"), new Version(1, 0, 0, 0)) == null, "equal version does not update");
        check(AutoUpdater.ParseRelease(Json("v0.9.0"), new Version(1, 0, 0, 0)) == null, "downgrade excluded");
        check(AutoUpdater.ParseRelease(Json("v99.1.0-beta"), new Version(1, 0, 0, 0)) == null, "nonstable version excluded");
        bool rejected = false;
        try { AutoUpdater.ParseRelease(Json(wrongHost: true), new Version(1, 0, 0, 0)); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "foreign update URL rejected");
        string digest = new('a', 64);
        check(AutoUpdater.ParseChecksum(digest + "  SmartLanguageSwitcher.exe\r\n") == digest, "release checksum parsed");
        rejected = false;
        try { AutoUpdater.ParseChecksum(digest + "  unrelated.exe"); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "unrelated checksum rejected");
        rejected = false;
        try { AutoUpdater.ParseChecksum(digest + "  SmartLanguageSwitcher.exe\n" + digest + "  SmartLanguageSwitcher.exe"); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "duplicate checksum rejected");
        string folder = Path.Combine(Path.GetTempPath(), "SLS-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "old file.exe"), staged = Path.Combine(folder, "new file.tmp"), backup = Path.Combine(folder, "old.bak");
        try
        {
            File.WriteAllText(target, "old"); File.WriteAllText(staged, "new");
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged)));
            check(AutoUpdater.VerifyHash(staged, hash), "download checksum verified");
            rejected = false;
            try { UpdateInstaller.Commit(staged, target, backup, digest); } catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllText(target) == "old", "bad hash cannot replace installed executable");
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                rejected = false;
                try { UpdateInstaller.Commit(staged, target, backup, hash); } catch (IOException) { rejected = true; }
                check(rejected, "locked target fails without overwriting");
            }
            UpdateInstaller.Commit(staged, target, backup, hash);
            check(File.ReadAllText(target) == "new" && File.ReadAllText(backup) == "old", "atomic replacement preserves rollback copy");
            File.Replace(backup, target, null);
            check(File.ReadAllText(target) == "old", "rollback restores original bytes");
            check(new Settings().AutoUpdate, "automatic updates enabled by default");
        }
        finally { foreach (var file in new[] { target, staged, backup }) UpdateInstaller.TryDelete(file); Directory.Delete(folder, false); }
    }
    public static int Fixture(string[] args)
    {
        string root = TestRoot!;
        if (args.Length == 2 && args[0] == "--update-ready")
        {
            if (Environment.GetEnvironmentVariable("SLS_UPDATE_TEST_FAIL") == "1") return 1;
            UpdateInstaller.SignalReady(args); return 0;
        }
        if (args.Length == 1 && args[0] == "--update-fixture")
        {
            string source = Path.Combine(root, "download.exe");
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var plan = UpdateInstaller.Prepare(Environment.ProcessPath!, source, hash, "v99.1.0", root);
            UpdateInstaller.LaunchAsync(plan, CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }
        File.WriteAllText(Path.Combine(root, "rollback-restarted"), "ready");
        return 0;
    }
    public static int Integration(string report)
    {
        var results = new List<string>(); int code = 0;
        try
        {
            foreach (bool fail in new[] { false, true })
            {
                string root = Path.Combine(UpdateInstaller.Cache, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                string executable = Path.Combine(root, "SmartLanguageSwitcher.exe");
                try
                {
                    File.Copy(Environment.ProcessPath!, executable);
                    File.Copy(Environment.ProcessPath!, Path.Combine(root, "download.exe"));
                    var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                    info.ArgumentList.Add("--update-fixture");
                    info.Environment["SLS_UPDATE_TEST_ROOT"] = root;
                    info.Environment["SLS_UPDATE_TEST_FAIL"] = fail ? "1" : "0";
                    using var fixture = Process.Start(info)!;
                    if (!fixture.WaitForExit(15000) || fixture.ExitCode != 0) throw new IOException("Fixture failed to hand off update.");
                    string completed = Path.Combine(root, "test-completed");
                    for (int i = 0; i < 250 && !File.Exists(completed); i++) Thread.Sleep(100);
                    string expected = fail ? "rollback" : "success";
                    if (!File.Exists(completed) || File.ReadAllText(completed) != expected) throw new IOException("Update did not complete: " + expected);
                    if (fail)
                    {
                        for (int i = 0; i < 50 && !File.Exists(Path.Combine(root, "rollback-restarted")); i++) Thread.Sleep(100);
                        if (!File.Exists(Path.Combine(root, "rollback-restarted"))) throw new IOException("Rollback did not restart original executable.");
                    }
                    results.Add("PASS isolated executable replacement and " + expected + " restart");
                }
                finally
                {
                    Thread.Sleep(300);
                    // This directory was created above solely for this fixture; never recurse.
                    foreach (var file in Directory.EnumerateFiles(root)) UpdateInstaller.TryDelete(file);
                    try { Directory.Delete(root, false); } catch (IOException) { }
                }
            }
        }
        catch (Exception e) { results.Add("FAIL " + e); code = 1; }
        File.WriteAllText(report, JsonSerializer.Serialize(new { exitCode = code, results }, new JsonSerializerOptions { WriteIndented = true }));
        return code;
    }
}
