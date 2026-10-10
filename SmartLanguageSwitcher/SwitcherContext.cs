using System.Drawing;
namespace SmartLanguageSwitcher;
internal sealed class SwitcherContext : ApplicationContext
{
    private readonly Settings settings = Settings.Load();
    private readonly NotifyIcon tray;
    private readonly KeyboardHook hook;
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 100 };
    private readonly Control dispatcher = new();
    private readonly LayoutHistory history;
    private SettingsForm? form;
    private bool enabled = true;
    private nint sessionWindow, target;
    private bool released;
    private long deadline;
    private string? status;
    public SwitcherContext()
    {
        var installed = Native.InstalledLayouts();
        history = new(Native.CurrentForegroundLayout(), Native.Resolve(settings.LastLayout, installed), Native.Resolve(settings.PreviousLayout, installed));
        dispatcher.CreateControl();
        tray = new() { Icon = SystemIcons.Application, Text = "Smart Language Switcher", Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        var pause = new ToolStripMenuItem("Pause") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => { enabled = !pause.Checked; EndSession(); tray.Text = enabled ? "Smart Language Switcher" : "Smart Language Switcher (paused)"; };
        menu.Items.Add(pause);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowSettings();
        // Queue all window queries, requests and disk work outside the low-level hook.
        hook = new(() => enabled && form == null, () => settings.Shortcut,
            () => dispatcher.BeginInvoke(Step), () => dispatcher.BeginInvoke(Release));
        poll.Tick += (_, _) => Observe();
        poll.Start();
    }
    private void Step()
    {
        var window = Native.GetForegroundWindow();
        if (history.Active && (window != sessionWindow || released)) EndSession();
        if (!history.Active) sessionWindow = window;
        target = history.Step(Native.CurrentForegroundLayout(), Native.InstalledLayouts());
        if (target == 0) return;
        released = false;
        deadline = Environment.TickCount64 + 800;
        if (!Native.ChangeForegroundLayout(target))
        {
            Warn("The foreground application could not receive the layout request.");
            EndSession();
        }
    }
    private void Release() { released = true; Observe(); }
    private void Observe()
    {
        var actual = Native.CurrentForegroundLayout();
        if (history.Active)
        {
            if (Native.GetForegroundWindow() != sessionWindow) { EndSession(); return; }
            if (released && actual == target) { EndSession(); return; }
            if (Environment.TickCount64 >= deadline && actual != target)
            {
                Warn("Layout change was not confirmed. The app or Windows may have rejected or overridden it.");
                EndSession();
            }
            return;
        }
        if (actual != 0 && actual != history.Current) { history.Observe(actual); Persist(); }
    }
    private void EndSession()
    {
        history.Finish(Native.CurrentForegroundLayout());
        target = 0; released = false;
        Persist();
    }
    private void Persist()
    {
        string? last = history.Current == 0 ? null : Native.LayoutId(history.Current);
        string? previous = history.Previous == 0 ? null : Native.LayoutId(history.Previous);
        if (settings.LastLayout == last && settings.PreviousLayout == previous) return;
        settings.LastLayout = last; settings.PreviousLayout = previous;
        if (!settings.Save()) Warn("Settings could not be saved. Check access to your application data folder.");
    }
    private void Warn(string message)
    {
        if (status == message) return;
        status = message;
        tray.ShowBalloonTip(5000, "Smart Language Switcher", message, ToolTipIcon.Warning);
    }
    private void ShowSettings()
    {
        EndSession();
        if (form == null || form.IsDisposed)
        {
            form = new SettingsForm(settings);
            form.FormClosed += (_, _) => form = null;
        }
        form.Show(); form.Activate();
    }
    protected override void ExitThreadCore()
    {
        EndSession(); poll.Dispose(); hook.Dispose(); form?.Dispose();
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); dispatcher.Dispose();
        base.ExitThreadCore();
    }
}
