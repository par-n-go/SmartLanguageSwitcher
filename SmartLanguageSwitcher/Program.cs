namespace SmartLanguageSwitcher;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--self-test") return SelfTest.Run(Path.GetFullPath(args[1]));
        using var mutex = new Mutex(true, @"Local\SmartLanguageSwitcher.SingleInstance", out bool owner);
        if (!owner) return 0;
        try { Application.Run(new SwitcherContext()); return 0; }
        catch (Exception e) { MessageBox.Show(e.Message, "Smart Language Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
}
