using System.ComponentModel;
using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc callback;
    private nint handle;
    private readonly ChordState state = new();
    public KeyboardHook(Func<bool> enabled, Func<ShortcutKind> shortcut, Action step, Action finish)
    {
        callback = (code, message, data) => {
            if (code >= 0 && (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN or Native.WM_KEYUP or Native.WM_SYSKEYUP)
            {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(data);
                if ((info.Flags & Native.LLKHF_INJECTED) != 0 && info.VkCode is 0x11 or 0xA2 or 0xA3)
                    state.Process(info.VkCode, (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN, false, shortcut());
                if ((info.Flags & Native.LLKHF_INJECTED) == 0)
                {
                    var result = state.Process(info.VkCode, (int)message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN, enabled(), shortcut());
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
    public void Dispose() { if (handle != 0) Native.UnhookWindowsHookEx(handle); handle = 0; }
}



