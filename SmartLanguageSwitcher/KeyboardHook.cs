using System.ComponentModel;
using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc callback;
    private readonly Action finish;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private Control? dispatcher;
    private ApplicationContext? context;
    private Exception? startupError;
    private nint handle;
    private ChordState state = new();
    private int configuration, disposed;
    private long lastInput, renewed;
    private int generation;
    private const nuint TestMarker = 0x534C5354;
    private readonly ManualResetEventSlim testSeen = new();
    public void Configure(bool enabled, ShortcutKind shortcut) => Volatile.Write(ref configuration, ((int)shortcut << 1) | (enabled ? 1 : 0));
    public KeyboardHook(bool enabled, ShortcutKind shortcut, Action step, Action finish)
    {
        this.finish = finish;
        Configure(enabled, shortcut);
        callback = (code, message, data) => {
            if (code >= 0 && (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN or Native.WM_KEYUP or Native.WM_SYSKEYUP)
            {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(data);
                if ((info.Flags & Native.LLKHF_INJECTED) != 0 && info.ExtraInfo == TestMarker && info.VkCode == 0xE8)
                { testSeen.Set(); return 1; }
                lastInput = Environment.TickCount64;
                bool down = (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                int config = Volatile.Read(ref configuration);
                var mode = (ShortcutKind)(config >> 1);
                if ((info.Flags & Native.LLKHF_INJECTED) != 0)
                {
                    if (!down || info.VkCode is 0x11 or 0xA2 or 0xA3)
                    {
                        var released = state.Process(info.VkCode, down, false, mode);
                        if (released.Finish) finish();
                    }
                }
                else
                {
                    var result = state.Process(info.VkCode, down, (config & 1) != 0, mode, Native.IsKeyDown);
                    if (result.MaskStart) Native.MaskStartMenu();
                    if (result.Finish) finish();
                    if (result.Step) step();
                    if (result.Suppress) return 1;
                }
            }
            return Native.CallNextHookEx(handle, code, message, data);
        };
        thread = new Thread(Run) { IsBackground = true, Name = "Keyboard hook" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (startupError != null) { thread.Join(); throw new InvalidOperationException("Keyboard hook could not start.", startupError); }
    }
    private void Run()
    {
        try
        {
            using var control = new Control();
            control.CreateControl(); dispatcher = control;
            using var loop = new ApplicationContext(); context = loop;
            Install();
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (_, _) => Maintain(); timer.Start();
            ready.Set();
            Application.Run(loop);
        }
        catch (Exception ex) { startupError = ex; ready.Set(); }
        finally { if (handle != 0) Native.UnhookWindowsHookEx(handle); handle = 0; }
    }
    private void Install()
    {
        var replacement = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, callback, 0, 0);
        if (replacement == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var previous = handle; handle = replacement;
        if (previous != 0) Native.UnhookWindowsHookEx(previous);
        renewed = Environment.TickCount64;
        Interlocked.Increment(ref generation);
    }
    private void Maintain()
    {
        if (state.Reconcile(Native.IsKeyDown)) finish();
        // Windows provides no notification when it removes a timed-out hook.
        // Renew during idle periods, never halfway through a shortcut or typing.
        long now = Environment.TickCount64;
        if (now - renewed < 30000 || now - lastInput < 500 || state.Active) return;
        for (uint key = 8; key < 256; key++) if (Native.IsKeyDown(key)) return;
        try { Install(); state = new(); }
        catch (Win32Exception) { renewed = now - 25000; }
    }
    internal void TestRecovery(Action<bool, string> check)
    {
        testSeen.Reset();
        check(KeyboardInput.Send(TestMarker, (0xE8, true)) && testSeen.Wait(2000), "hook receives input while caller has no message loop");
        int before = Volatile.Read(ref generation);
        dispatcher!.Invoke(() => { Native.UnhookWindowsHookEx(handle); renewed = Environment.TickCount64 - 30000; });
        check(SpinWait.SpinUntil(() => Volatile.Read(ref generation) > before, 5000), "idle watchdog reinstalls a removed hook");
        testSeen.Reset();
        check(KeyboardInput.Send(TestMarker, (0xE8, true)) && testSeen.Wait(2000), "reinstalled hook receives input without app restart");
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (thread.IsAlive) { dispatcher!.BeginInvoke(() => context!.ExitThread()); thread.Join(); }
        ready.Dispose(); testSeen.Dispose();
    }
}
