using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal static class KeyboardInput
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    public static bool Send(nuint marker, params (ushort Key, bool Up)[] keys)
    {
        var inputs = keys.Select(key => new Input { Type = 1, Data = new InputUnion {
            Keyboard = new Keyboard { Key = key.Key, Flags = (key.Up ? 2u : 0u) | (key.Key is 0x5B or 0x5C ? 1u : 0u), Extra = marker }
        }}).ToArray();
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }
}
