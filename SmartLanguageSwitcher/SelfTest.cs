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
            foreach (uint modifier in new uint[] { 0x5B, 0x5C, 0xA4, 0xA2 })
            {
                var mode = modifier == 0xA4 ? ShortcutKind.AltShift : modifier == 0xA2 ? ShortcutKind.CtrlShift : ShortcutKind.WinSpace;
                uint trigger = mode == ShortcutKind.WinSpace ? 0x20u : 0xA0u;
                var recovered = new ChordState();
                recovered.Process(modifier, true, true, mode);
                recovered.Process(trigger, true, true, mode);
                recovered.Process(trigger, false, true, mode);
                var plain = recovered.Process(trigger, true, true, mode, _ => false);
                Check(!plain.Step && !plain.Suppress && plain.Finish, "missed modifier release does not consume typing " + modifier);
                recovered.Process(trigger, false, true, mode);
                Check(recovered.Process(modifier, true, true, mode, _ => false).Step == false, "current modifier down ignores old async state " + modifier);
                Check(recovered.Process(trigger, true, true, mode, k => k == modifier).Step, "fresh physical chord works after recovery " + modifier);
                recovered.Process(trigger, false, true, mode);
                Check(recovered.Process(modifier, false, false, mode).Finish, "synthetic release ends observed session " + modifier);
                Check(!recovered.Process(trigger, true, true, mode, _ => false).Step, "typing after synthetic release does not switch " + modifier);
            }
            var snapshotOnly = new ChordState();
            foreach (uint shift in new uint[] { 0x10, 0xA0, 0xA1 })
            {
                var missedShift = new ChordState();
                missedShift.Process(shift, true, true, ShortcutKind.WinSpace);
                missedShift.Process(0x5B, true, true, ShortcutKind.WinSpace);
                Check(missedShift.Process(0x20, true, true, ShortcutKind.WinSpace, key => key == 0x5B).Step,
                    "missed Shift release no longer blocks Win+Space " + shift);
                var heldShift = new ChordState();
                heldShift.Process(shift, true, true, ShortcutKind.WinSpace);
                heldShift.Process(0x5B, true, true, ShortcutKind.WinSpace);
                Check(!heldShift.Process(0x20, true, true, ShortcutKind.WinSpace, key => key == shift || key == 0x5B).Step,
                    "physically held Shift still excludes Win+Space " + shift);
                var consumedShift = new ChordState();
                consumedShift.Process(0xA4, true, true, ShortcutKind.AltShift);
                consumedShift.Process(shift, true, true, ShortcutKind.AltShift);
                consumedShift.Reconcile(key => key == 0xA4);
                Check(!consumedShift.Process(shift, true, true, ShortcutKind.AltShift).Step,
                    "suppressed Shift stays held despite async state until release " + shift);
            }
            Check(!snapshotOnly.Process(0x20, true, true, ShortcutKind.WinSpace, _ => true).Step, "snapshot cannot arm an unobserved Win chord");
            var bothWin = new ChordState();
            bothWin.Process(0x5B, true, true, ShortcutKind.WinSpace);
            bothWin.Process(0x5C, true, true, ShortcutKind.WinSpace);
            Check(bothWin.Process(0x20, true, true, ShortcutKind.WinSpace, key => key == 0x5C).Step, "recovery keeps the other held Win key");
            Check(bothWin.Reconcile(_ => false) && !bothWin.Reconcile(_ => false), "idle reconciliation finishes once");
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
            var filteredSettings = new Settings { ExcludeBritishEnglish = true };
            nint us = 0x04090409, uk = 0x04090809, ru = 0x04190419, he = 0x040d040d;
            nint[] allLayouts = [us, uk, ru, he];
            var filtered = new LayoutHistory(us, us, uk, filteredSettings.AllowsLayout);
            Check(filtered.Previous == 0 && filtered.Step(us, allLayouts) == ru, "excluded UK cannot be previous layout");
            Check(filtered.Step(ru, allLayouts) == he && filtered.Step(he, allLayouts) == us, "cycle skips UK and keeps US Russian Hebrew");
            filtered.Finish(ru); filtered.Observe(uk);
            Check(filtered.Current == ru && filtered.Previous == us, "external UK does not replace recent history");
            Check(filtered.Step(uk, allLayouts) == ru, "leaving external UK returns to most recent allowed layout");
            Check(filtered.Step(ru, allLayouts) == us && filtered.Step(us, allLayouts) == he && filtered.Step(he, allLayouts) == ru,
                "cycle started from excluded UK never returns to UK");
            Check(new Settings().AllowsLayout(uk), "UK remains allowed by default");
            Check(ShortcutTrace.ParseExpiry("2026-10-22T18:47:06.0070483Z\r\n") == new DateTime(2026, 10, 22, 18, 47, 6, DateTimeKind.Utc).AddTicks(70483), "diagnostic expiry parses invariant roundtrip timestamp with newline");
            Check(ShortcutTrace.ParseExpiry("invalid") == default, "invalid diagnostic expiry disables logging");
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
                var settings = new Settings { Shortcut = ShortcutKind.CtrlShift, LastLayout = "1", PreviousLayout = "2", ExcludeBritishEnglish = true };
                Check(settings.Save(temp), "settings save");
                Check(Settings.Load(temp).PreviousLayout == "2", "settings roundtrip");
                Check(Settings.Load(temp).ExcludeBritishEnglish, "UK exclusion persists");
                File.WriteAllText(temp, "{broken");
                Check(Settings.Load(temp).Shortcut == ShortcutKind.AltShift, "corrupt settings recovery");
                File.WriteAllText(temp, "{\"Shortcut\":999}");
                Check(Settings.Load(temp).Shortcut == ShortcutKind.AltShift, "invalid shortcut recovery");
            }
            finally { File.Delete(temp); }
            using (var hook = new KeyboardHook(false, ShortcutKind.AltShift, () => { }, () => { }))
            {
                Check(true, "Windows low-level hook installs and disposes");
                hook.TestRecovery(Check);
            }
            int manualChecks = 0;
            using (var settingsForm = new SettingsForm(new() { AutoUpdate = false }, render: true,
                checkUpdates: () => { manualChecks++; return Task.CompletedTask; }))
            {
                settingsForm.Opacity = 0; settingsForm.ShowInTaskbar = false; settingsForm.Show();
                Check(settingsForm.Icon != null, "settings form and custom icon initialize");
                var checkButton = settingsForm.Controls.OfType<Button>().Single(c => c.Text == "Check for updates");
                checkButton.PerformClick(); checkButton.PerformClick();
                Check(manualChecks == 2 && checkButton.Enabled, "manual update button works repeatedly with automatic updates disabled");
                settingsForm.Close();
            }
            UpdateTests.Unit(Check);
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
