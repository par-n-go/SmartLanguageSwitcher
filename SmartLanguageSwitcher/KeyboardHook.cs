using System.ComponentModel;
using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc callback;
    private readonly Action finish;
    private nint handle;
    private readonly ChordState state = new();
    public KeyboardHook(Func<bool> enabled, Func<ShortcutKind> shortcut, Action step, Action finish)
    {
        this.finish = finish;
        callback = (code, message, data) => {
            if (code >= 0 && (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN or Native.WM_KEYUP or Native.WM_SYSKEYUP)
            {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(data);
                bool down = (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                if ((info.Flags & Native.LLKHF_INJECTED) != 0)
                {
                    // Remappers and the shell can synthesize releases. Never leave an observed
                    // modifier held just because its release was injected. Ctrl also guards AltGr.
                    if (!down || info.VkCode is 0x11 or 0xA2 or 0xA3)
                    {
                        var released = state.Process(info.VkCode, down, false, shortcut());
                        if (released.Finish) finish();
                    }
                }
                else
                {
                    var result = state.Process(info.VkCode, down, enabled(), shortcut(), Native.IsKeyDown);
                    if (result.MaskStart) Native.MaskStartMenu();
                    if (result.Finish) finish();
                    if (result.Step) step();
                    if (result.Suppress) return 1;
                }
            }
            return Native.CallNextHookEx(handle, code, message, data);
        };
        handle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, callback, 0, 0);
        if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Reconcile() { if (state.Reconcile(Native.IsKeyDown)) finish(); }
    public void Dispose() { if (handle != 0) Native.UnhookWindowsHookEx(handle); handle = 0; }
}
