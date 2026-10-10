using System.Runtime.InteropServices;

namespace SmartLanguageSwitcher;

internal static class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    public const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const uint LLKHF_INJECTED = 0x10;

    public delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    public static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")] public static extern nint ActivateKeyboardLayout(nint layout, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint LoadKeyboardLayout(string id, uint flags);
    [DllImport("user32.dll")] public static extern bool UnloadKeyboardLayout(nint layout);
    public static string LayoutId(nint layout) => unchecked((ulong)layout.ToInt64()).ToString("X16");

    // The complete HKL can change after a reboot. Prefer exact match; then match the low 32 bits of the HKL (not a stable KLID for every IME).
    public static nint Resolve(string? id, IReadOnlyList<nint> installed)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        foreach (var hkl in installed)
            if (LayoutId(hkl).Equals(id, StringComparison.OrdinalIgnoreCase)) return hkl;
        if (ulong.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out var raw))
            foreach (var hkl in installed)
                if ((unchecked((ulong)hkl.ToInt64()) & 0xFFFFFFFF) == (raw & 0xFFFFFFFF)) return hkl;
        return 0;
    }

    public static List<nint> InstalledLayouts()
    {
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0) return [];
        var array = new nint[count];
        var fetched = GetKeyboardLayoutList(count, array);
        return array.Take(Math.Max(0, fetched)).Distinct().ToList();
    }

    public static nint CurrentForegroundLayout()
    {
        var window = GetForegroundWindow();
        if (window == 0) return 0;
        var threadId = GetWindowThreadProcessId(window, out _);
        return threadId == 0 ? 0 : GetKeyboardLayout(threadId);
    }

    public static bool ChangeForegroundLayout(nint layout)
    {
        var window = GetForegroundWindow();
        if (window == 0) return false;
        // Target foreground focus control if accessible; fallback to foreground top-level window.
        uint thread = GetWindowThreadProcessId(window, out _);
        var gi = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (thread != 0 && GetGUIThreadInfo(thread, ref gi) && gi.Focus != 0)
            window = gi.Focus;
        return PostMessage(window, WM_INPUTLANGCHANGEREQUEST, 0, layout);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public System.Drawing.Rectangle CaretRect;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
}


