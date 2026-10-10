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
    private readonly AutoUpdater updater;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 30000 };
    private SettingsForm? form;
    private bool enabled = true;
    private nint sessionWindow, target;
    private bool released;
    private long deadline;
    private string? status;
    public SwitcherContext(Action? ready = null)
    {
        var installed = Native.InstalledLayouts();
        history = new(Native.CurrentForegroundLayout(), Native.Resolve(settings.LastLayout, installed), Native.Resolve(settings.PreviousLayout, installed));
        dispatcher.CreateControl();
        tray = new() { Icon = AppIcon.Load(), Text = "Smart Language Switcher", Visible = true };
        updater = new AutoUpdater(() => settings.AutoUpdate, () => form == null && !history.Active && !Native.IsKeyDown(0x5B) && !Native.IsKeyDown(0x5C) && !Native.IsKeyDown(0x12) && !Native.IsKeyDown(0x11), ExitThread, UpdateStatus);
        var menu = TrayMenu.Create(ShowSettings, paused => {
            enabled = !paused; EndSession();
            tray.Text = enabled ? "Smart Language Switcher" : "Smart Language Switcher (paused)";
        }, ExitThread, () => _ = updater.CheckAsync(manual: true));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowSettings();
        // Queue all window queries, requests and disk work outside the low-level hook.
        hook = new(() => enabled && form == null, () => settings.Shortcut,
            QueueStep, () => dispatcher.BeginInvoke(Release));
        poll.Tick += (_, _) => Observe();
        poll.Start();
        updateTimer.Tick += (_, _) => { updateTimer.Interval = 6 * 60 * 60 * 1000; _ = updater.CheckAsync(); };
        updateTimer.Start();
        _ = Task.Run(UpdateInstaller.CleanOldFiles);
        if (ready != null) dispatcher.BeginInvoke(ready);
    }
    private void QueueStep()
    {
        var expectedWindow = Native.GetForegroundWindow();
        var expectedShortcut = settings.Shortcut;
        long requestedAt = Environment.TickCount64;
        dispatcher.BeginInvoke(() => {
            if (!enabled || form != null || settings.Shortcut != expectedShortcut ||
                expectedWindow == 0 || Native.GetForegroundWindow() != expectedWindow ||
                Environment.TickCount64 - requestedAt > 250) return;
            Step();
        });
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
        hook.Reconcile();
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
    private void UpdateStatus(string message)
    {
        if (form != null && !form.IsDisposed) form.SetUpdateStatus(message);
        else tray.ShowBalloonTip(5000, "Smart Language Switcher", message, ToolTipIcon.Info);
    }
    private void ShowSettings()
    {
        EndSession();
        if (form == null || form.IsDisposed)
        {
            form = new SettingsForm(settings, checkUpdates: () => updater.CheckAsync(manual: true));
            form.FormClosed += (_, _) => form = null;
        }
        form.Show(); form.Activate();
    }
    protected override void ExitThreadCore()
    {
        updateTimer.Dispose(); updater.Dispose();
        EndSession(); poll.Dispose(); hook.Dispose(); form?.Dispose();
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); dispatcher.Dispose();
        base.ExitThreadCore();
    }
}
