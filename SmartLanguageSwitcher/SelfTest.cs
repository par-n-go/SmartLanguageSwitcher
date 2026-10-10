using System.Text.Json;
namespace SmartLanguageSwitcher;
internal static class SelfTest
{
    public static int Run(string reportPath)
    {
        var results = new List<string>();
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; results.Add("PASS " + name); }
        int exit = 0;
        try
        {
            foreach (var mode in Enum.GetValues<ShortcutKind>())
            {
                uint modifier = mode == ShortcutKind.AltShift ? 0xA4u : mode == ShortcutKind.CtrlShift ? 0xA2u : 0x5Bu;
                uint trigger = mode == ShortcutKind.WinSpace ? 0x20u : 0xA0u;
                var chord = new ChordState();
                Check(!chord.Process(modifier, true, true, mode).Suppress, mode + " modifier passes");
                var first = chord.Process(trigger, true, true, mode);
                Check(first.Step && first.Suppress, mode + " first press");
                Check(first.MaskStart == (mode == ShortcutKind.WinSpace), mode + " menu masking only for Win+Space");
                var repeat = chord.Process(trigger, true, true, mode);
                Check(!repeat.Step && repeat.Suppress, mode + " repeat suppressed");
                Check(chord.Process(trigger, false, true, mode).Suppress, mode + " trigger release suppressed");
                Check(chord.Process(trigger, true, true, mode).Step, mode + " next press");
                Check(chord.Process(modifier, false, true, mode).Finish, mode + " modifier release commits");
                Check(chord.Process(trigger, false, false, mode).Suppress, mode + " late release paired while disabled");
            }
            foreach (uint win in new uint[] { 0x5B, 0x5C })
            {
                var chord = new ChordState();
                Check(!chord.Process(win, true, true, ShortcutKind.WinSpace).MaskStart, "Win alone is not masked " + win);
                Check(chord.Process(0x20, true, true, ShortcutKind.WinSpace).MaskStart, "Win+Space requests mask " + win);
                Check(chord.Process(win, true, true, ShortcutKind.WinSpace).Suppress, "Win repeat does not re-arm Start " + win);
                Check(!chord.Process(win, false, true, ShortcutKind.WinSpace).Suppress, "Win release passes to prevent stuck key " + win);
                var paused = new ChordState();
                paused.Process(win, true, false, ShortcutKind.WinSpace);
                Check(!paused.Process(0x20, true, false, ShortcutKind.WinSpace).MaskStart, "paused chord not masked " + win);
            }
            var dual = new ChordState();
            dual.Process(0xA4, true, true, ShortcutKind.AltShift);
            dual.Process(0xA5, true, true, ShortcutKind.AltShift);
            dual.Process(0xA0, true, true, ShortcutKind.AltShift);
            Check(!dual.Process(0xA4, false, true, ShortcutKind.AltShift).Finish, "both Alt keys tracked independently");
            Check(dual.Process(0xA5, false, true, ShortcutKind.AltShift).Finish, "last Alt release commits");
            var altgr = new ChordState();
            altgr.Process(0xA2, true, true, ShortcutKind.AltShift);
            altgr.Process(0xA5, true, true, ShortcutKind.AltShift);
            Check(!altgr.Process(0xA0, true, true, ShortcutKind.AltShift).Suppress, "Ctrl+Alt+Shift passes");
            var history = new LayoutHistory(1, 1, 2);
            nint[] layouts = [1, 2, 3];
            Check(history.Step(1, layouts) == 2, "previous layout first");
            Check(history.Step(2, layouts) == 3, "cycle third layout");
            Check(history.Current == 1 && history.Previous == 2, "no intermediate history writes");
            history.Finish(3);
            Check(history.Current == 3 && history.Previous == 1, "confirmed selection committed");
            Check(history.Step(3, layouts) == 1, "next session toggles back");
            history.Finish(3);
            Check(history.Current == 3 && history.Previous == 1, "rejected request preserves history");
            history.Observe(2);
            Check(history.Current == 2 && history.Previous == 3, "external layout observed");
            Check(new LayoutHistory(3, 2, 1).Previous == 2, "restart uses last layout if foreground differs");
            var wrap = new LayoutHistory(1, 1, 2);
            wrap.Step(1, layouts); wrap.Step(2, layouts);
            Check(wrap.Step(3, layouts) == 1 && wrap.Step(1, layouts) == 2, "cycle wraps through origin");
            Check(new LayoutHistory(1, 1, 8).Step(1, layouts) == 2, "removed previous falls back");
            Check(new LayoutHistory(1, 1, 0).Step(1, [1]) == 0, "one layout no-op");
            string temp = Path.Combine(Path.GetTempPath(), "SmartLanguageSwitcher-test-" + Guid.NewGuid() + ".json");
            try
            {
                var settings = new Settings { Shortcut = ShortcutKind.CtrlShift, LastLayout = "1", PreviousLayout = "2" };
                Check(settings.Save(temp), "settings save");
                Check(Settings.Load(temp).PreviousLayout == "2", "settings roundtrip");
                File.WriteAllText(temp, "{broken");
                Check(Settings.Load(temp).Shortcut == ShortcutKind.AltShift, "corrupt settings recovery");
                File.WriteAllText(temp, "{\"Shortcut\":999}");
                Check(Settings.Load(temp).Shortcut == ShortcutKind.AltShift, "invalid shortcut recovery");
            }
            finally { File.Delete(temp); }
            using (var hook = new KeyboardHook(() => false, () => ShortcutKind.AltShift, () => { }, () => { }))
                Check(true, "Windows low-level hook installs and disposes");
            using (var settingsForm = new SettingsForm(new())) { settingsForm.CreateControl(); Check(settingsForm.Controls.Count == 10 && settingsForm.Icon != null, "settings form and custom icon initialize"); }
            var installed = Native.InstalledLayouts();
            nint temporaryLayout = 0;
            if (installed.Count < 2) { temporaryLayout = Native.LoadKeyboardLayout("0000040C", 0); installed = Native.InstalledLayouts(); }
            results.Add("Installed HKLs: " + string.Join(", ", installed.Select(Native.LayoutId)));
            Check(installed.Count > 1, "multiple installed layouts available for integration test");
            using var window = new Form { Text = "Smart Language Switcher validation", Width = 440, Height = 160, ShowInTaskbar = false };
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            int index = 0;
            nint original = Native.GetKeyboardLayout(0);
            long deadline = 0;
            window.Shown += (_, _) => {
                Native.PostMessage(window.Handle, Native.WM_INPUTLANGCHANGEREQUEST, 0, installed[index]);
                deadline = Environment.TickCount64 + 3000; timer.Start();
            };
            timer.Tick += (_, _) => {
                try
                {
                    if (Native.GetKeyboardLayout(0) == installed[index])
                    {
                        Check(true, "Windows confirms HKL " + Native.LayoutId(installed[index]));
                        if (++index == installed.Count) { timer.Stop(); window.Close(); return; }
                        Native.PostMessage(window.Handle, Native.WM_INPUTLANGCHANGEREQUEST, 0, installed[index]);
                        deadline = Environment.TickCount64 + 3000;
                    }
                    else if (Environment.TickCount64 > deadline) throw new InvalidOperationException("Windows layout request timed out");
                }
                catch (Exception e) { results.Add("FAIL " + e.Message); exit = 1; timer.Stop(); window.Close(); }
            };
            Application.Run(window);
            Native.ActivateKeyboardLayout(original, 0);
            if (temporaryLayout != 0) Native.UnloadKeyboardLayout(temporaryLayout);
        }
        catch (Exception e) { results.Add("FAIL " + e); exit = 1; }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { exitCode = exit, checks, os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(), results }, new JsonSerializerOptions { WriteIndented = true }));
        return exit;
    }
}



