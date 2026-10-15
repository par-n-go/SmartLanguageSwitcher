namespace SmartLanguageSwitcher;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--apply-update") return UpdateInstaller.Apply(args[1]);
        if (UpdateTests.IsTestProcess) return UpdateTests.Fixture(args);
        if (args.Length == 2 && args[0] == "--test-update-install") return UpdateTests.Integration(Path.GetFullPath(args[1]));
        if (args.Length == 2 && args[0] == "--test-update-network")
        {
            using var updater = new AutoUpdater(() => false, () => false, () => { }, _ => { });
            return updater.ProbeAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();
        }
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--self-test") return SelfTest.Run(Path.GetFullPath(args[1]));
        if (args.Length == 1 && args[0] == "--preview-settings") { Application.Run(new SettingsForm(Settings.Load(), preview: true)); return 0; }
        if (args.Length == 2 && args[0] == "--render-preview") { PreviewRenderer.Render(Path.GetFullPath(args[1])); return 0; }
        using var mutex = new Mutex(true, @"Local\SmartLanguageSwitcher.SingleInstance", out bool owner);
        if (!owner) return 0;
        try { ShortcutTrace.Initialize(); Application.Run(new SwitcherContext(() => UpdateInstaller.SignalReady(args))); return 0; }
        catch (Exception e) { MessageBox.Show(e.Message, "Smart Language Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
}
