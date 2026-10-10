namespace SmartLanguageSwitcher;

internal sealed class ChordState
{
    private readonly HashSet<uint> held = [];
    private readonly HashSet<uint> swallowed = [];
    private ShortcutKind? session;
    public bool Active => session != null;
    private bool Alt => held.Contains(0xA4) || held.Contains(0xA5) || held.Contains(0x12);
    private bool Ctrl => held.Contains(0xA2) || held.Contains(0xA3) || held.Contains(0x11);
    private bool Win => held.Contains(0x5B) || held.Contains(0x5C);
    private bool Shift => held.Contains(0xA0) || held.Contains(0xA1) || held.Contains(0x10);
    public (bool Suppress, bool Step, bool Finish, bool MaskStart) Process(uint key, bool down, bool enabled, ShortcutKind shortcut)
    {
        bool fresh = down ? held.Add(key) : held.Remove(key);
        bool suppress = swallowed.Contains(key);
        if (!down) swallowed.Remove(key);
        bool finish = session is { } mode && !(mode switch {
            ShortcutKind.AltShift => Alt, ShortcutKind.CtrlShift => Ctrl, _ => Win });
        if (finish) session = null;
        bool trigger = shortcut switch {
            ShortcutKind.AltShift => key is 0x10 or 0xA0 or 0xA1 && Alt && !Ctrl && !Win,
            ShortcutKind.CtrlShift => key is 0x10 or 0xA0 or 0xA1 && Ctrl && !Alt && !Win,
            _ => key == 0x20 && Win && !Alt && !Ctrl && !Shift
        };
        bool step = down && fresh && enabled && trigger;
        if (step) { session = shortcut; swallowed.Add(key); suppress = true; }
        bool maskStart = step && shortcut == ShortcutKind.WinSpace;
        // A repeated Win-down after a consumed chord must not re-arm Start.
        if (session == ShortcutKind.WinSpace && down && key is 0x5B or 0x5C)
        {
            if (!fresh) suppress = true;
            else maskStart = true;
        }
        return (suppress, step, finish, maskStart);
    }
}

