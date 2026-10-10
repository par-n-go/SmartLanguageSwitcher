using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SmartLanguageSwitcher;
internal sealed record ReleaseInfo(Version Version, string Tag, string ExeUrl, string HashUrl, long Size, string? Digest);
internal sealed class AutoUpdater : IDisposable
{
    internal const string Repository = "https://github.com/par-n-go/SmartLanguageSwitcher";
    internal static Version CurrentVersion => typeof(Program).Assembly.GetName().Version!;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly CancellationTokenSource stopping = new();
    private readonly Func<bool> enabled, idle;
    private readonly Action exit;
    private readonly Action<string> notify;
    private bool busy;
    public AutoUpdater(Func<bool> enabled, Func<bool> idle, Action exit, Action<string> notify)
    {
        this.enabled = enabled; this.idle = idle; this.exit = exit; this.notify = notify;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartLanguageSwitcher/" + CurrentVersion.ToString(3));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    internal static ReleaseInfo? ParseRelease(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag[1..] + ".0", out var version) || version <= current) return null;
        string baseUrl = Repository + "/releases/download/" + tag + "/";
        JsonElement Asset(string name) => root.GetProperty("assets").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name);
        var exe = Asset("SmartLanguageSwitcher.exe"); var hash = Asset("SHA256SUMS.txt");
        if (exe.GetProperty("browser_download_url").GetString() != baseUrl + "SmartLanguageSwitcher.exe" ||
            hash.GetProperty("browser_download_url").GetString() != baseUrl + "SHA256SUMS.txt" ||
            exe.GetProperty("state").GetString() != "uploaded" || hash.GetProperty("state").GetString() != "uploaded")
            throw new InvalidDataException("Unexpected release assets.");
        long size = exe.GetProperty("size").GetInt64();
        if (size < 1024 || size > 200 * 1024 * 1024) throw new InvalidDataException("Invalid update size.");
        return new(version, tag, baseUrl + "SmartLanguageSwitcher.exe", baseUrl + "SHA256SUMS.txt", size,
            exe.TryGetProperty("digest", out var digest) ? digest.GetString() : null);
    }
    internal static string ParseChecksum(string text)
    {
        var matches = Regex.Matches(text.TrimStart('\uFEFF'), @"(?m)^([a-fA-F0-9]{64})[ \t]+\*?SmartLanguageSwitcher\.exe\r?$");
        if (matches.Count != 1) throw new InvalidDataException("Missing or ambiguous checksum.");
        return matches[0].Groups[1].Value.ToLowerInvariant();
    }
    internal static bool VerifyHash(string file, string expected)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
    private async Task<byte[]> Download(string url, int limit, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var final = response.RequestMessage!.RequestUri!;
        if (final.Scheme != "https" || final.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com"))
            throw new InvalidDataException("Unexpected download destination.");
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Download too large.");
        using var memory = new MemoryStream();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, token)) != 0)
        {
            if (memory.Length + read > limit) throw new InvalidDataException("Download too large.");
            await memory.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return memory.ToArray();
    }
    internal async Task<int> ProbeAsync(string report)
    {
        string? directory = null;
        try
        {
            var json = await Download("https://api.github.com/repos/par-n-go/SmartLanguageSwitcher/releases/latest", 1024 * 1024, stopping.Token);
            var release = ParseRelease(System.Text.Encoding.UTF8.GetString(json), new Version(0, 0, 0, 0)) ?? throw new InvalidDataException("No stable release.");
            var hash = ParseChecksum(System.Text.Encoding.UTF8.GetString(await Download(release.HashUrl, 4096, stopping.Token)));
            var bytes = await Download(release.ExeUrl, (int)release.Size, stopping.Token);
            if (bytes.LongLength != release.Size || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase) ||
                (release.Digest != null && !release.Digest.Equals("sha256:" + hash, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Downloaded release verification failed.");
            directory = Path.Combine(UpdateInstaller.Cache, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "download.exe"); await File.WriteAllBytesAsync(file, bytes);
            if (Version.Parse(FileVersionInfo.GetVersionInfo(file).FileVersion!) != release.Version) throw new InvalidDataException("File version mismatch.");
            File.WriteAllText(report, JsonSerializer.Serialize(new { exitCode = 0, release.Tag, release.Size, sha256 = hash, result = "GitHub API, HTTPS download, size, digest and executable version verified; no installation performed." }));
            return 0;
        }
        catch (Exception e) { File.WriteAllText(report, JsonSerializer.Serialize(new { exitCode = 1, error = e.Message })); return 1; }
        finally { if (directory != null) UpdateInstaller.CleanDirectory(directory); }
    }
    public async Task CheckAsync(bool manual = false)
    {
        if (busy) { if (manual) notify("An update check is already running."); return; }
        if (!manual && !enabled()) return;
        busy = true; string? directory = null; UpdatePlan? plan = null; bool handedOff = false;
        try
        {
            string process = Environment.ProcessPath ?? "";
            if (!process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(process).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Updates require the published EXE.");
            var token = stopping.Token;
            var json = await Download("https://api.github.com/repos/par-n-go/SmartLanguageSwitcher/releases/latest", 1024 * 1024, token);
            var release = ParseRelease(System.Text.Encoding.UTF8.GetString(json), CurrentVersion);
            if (release == null) { if (manual) notify("You're up to date."); return; }
            if (!manual && File.Exists(UpdateInstaller.FailedVersionPath) && File.ReadAllText(UpdateInstaller.FailedVersionPath) == release.Tag) return;
            if (manual) notify("Downloading update…");
            var checksum = ParseChecksum(System.Text.Encoding.UTF8.GetString(await Download(release.HashUrl, 4096, token)));
            if (release.Digest != null && !release.Digest.Equals("sha256:" + checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release checksums disagree.");
            directory = Path.Combine(UpdateInstaller.Cache, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string download = Path.Combine(directory, "download.exe");
            var data = await Download(release.ExeUrl, (int)release.Size, token);
            if (data.LongLength != release.Size) throw new InvalidDataException("Incomplete update.");
            await File.WriteAllBytesAsync(download, data, token);
            if (!await Task.Run(() => VerifyHash(download, checksum), token)) throw new InvalidDataException("Update checksum failed.");
            if (!Version.TryParse(FileVersionInfo.GetVersionInfo(download).FileVersion, out var actualVersion) || actualVersion != release.Version)
                throw new InvalidDataException("Update version does not match release.");
            if (manual && !idle()) notify("Update ready. Close Settings to install.");
            for (int i = 0; !idle() && i < 300; i++) await Task.Delay(2000, token);
            if (!idle() || (!manual && !enabled())) { if (manual) notify("Update deferred. Check again when ready."); return; }
            plan = await Task.Run(() => UpdateInstaller.Prepare(process, download, checksum, release.Tag, directory), token);
            if (!idle() || (!manual && !enabled())) return;
            await UpdateInstaller.LaunchAsync(plan, token, () => idle() && (manual || enabled()));
            handedOff = true;
            notify("Update ready. Restarting…");
            exit();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (!stopping.IsCancellationRequested)
            {
                UpdateInstaller.WriteError(e.Message);
                notify("Update couldn't finish. It will retry later.");
            }
        }
        finally
        {
            if (!handedOff)
            {
                if (plan != null) UpdateInstaller.TryDelete(plan.Staged);
                if (directory != null) UpdateInstaller.CleanDirectory(directory);
            }
            busy = false;
        }
    }
    public void Dispose() { stopping.Cancel(); client.Dispose(); stopping.Dispose(); }
}

