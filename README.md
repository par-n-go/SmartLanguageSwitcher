# Smart Language Switcher

A Windows 11 tray application in C# / .NET 10. It keeps a global recent-layout pair while observing the keyboard layout of the foreground window.

## Download and run

Download `SmartLanguageSwitcher.exe` from [Releases](https://github.com/par-n-go/SmartLanguageSwitcher/releases). The Windows x64 executable includes .NET; a separate runtime installation is unnecessary. It is unsigned. Put it in a permanent folder before enabling **Start with Windows** in the tray settings.

Double-click the tray icon for settings. Right-click for Settings, Pause, or Exit. Only one normal instance runs in each Windows session.

## Switching

Choose Alt+Shift (default), Ctrl+Shift, or Win+Space (best effort) in settings. Hold the first key **before** pressing the second key. The first press selects the previous layout. Further distinct presses visit the remaining installed layouts in Windows order, then the starting layout, and wrap around. Keyboard auto-repeat does not cycle. Release the held modifier to commit the layout Windows actually reports. With no usable previous layout, the first other installed layout is used.

Left and right modifier keys are tracked independently. Ctrl+Alt combinations are excluded to protect AltGr typing. Synthetic keystrokes do not activate switching. The app consumes the selected trigger's key-downs, repeats and paired key-up to reduce native double switching. Modifier keys themselves pass through; Alt menu activation or the Windows Start menu can still occur on some systems. Shift-first chords are left to Windows.

## Avoid native hotkey conflicts

For **both Alt+Shift and Ctrl+Shift**, disable the corresponding Windows shortcut:

Settings → Time & language → Typing → Advanced keyboard settings → Input language hot keys → Advanced Key Settings → Between input languages → Change Key Sequence → Not Assigned.

Also check any per-language key sequences and other keyboard utilities. The app does not change system hotkey settings automatically. Suppression alone is not a guarantee against Windows or another hook changing the layout.

**Win+Space is reserved by Windows.** There is no supported guarantee that a low-level hook will suppress its flyout or native switch. This mode is best effort; if the shell intervenes, select Alt+Shift or Ctrl+Shift instead. Physical Win+Space behavior across Windows builds has not been validated by the automated tests.

## History, settings and startup

The foreground thread's HKL is sampled every 100 ms and immediately when a switching session starts. External changes and changes caused by focusing another application update the global pair. Intermediate layouts during a held-key session are excluded from history. A session ends if focus changes. The final request gets up to 800 ms to be confirmed; rejection or a different observed layout produces a tray warning, and history follows the observed layout instead of assuming success. Very brief changes between samples can be missed.

Settings and the last two HKLs are saved with an atomic file replacement in `%APPDATA%\SmartLanguageSwitcher\settings.json`. Invalid JSON falls back to defaults. Removed layouts are ignored. On restart, if the foreground layout differs from the saved last layout, that saved last layout becomes the previous one. HKLs are handles, not universally stable keyboard-profile identifiers; IME/profile changes across reboots may require a new history pair.

Startup uses the current user's `Software\Microsoft\Windows\CurrentVersion\Run` registry value named `SmartLanguageSwitcher`. Enable it only after placing the published EXE in a permanent folder. Windows Startup Apps can separately disable a registered entry. To uninstall, uncheck startup, exit, delete the EXE, and optionally remove the settings folder.

## Compatibility limits

Layout changes use `WM_INPUTLANGCHANGEREQUEST`, which the target application may reject. Foreground HKL tracking does not fully represent TSF input profiles or IME composition modes. Elevated applications, games, remote sessions, secure desktop/UAC, and applications using custom input systems are not guaranteed. This app requests no elevation. It does not install a service or driver, record typed text, or communicate over the network.

A low-level hook can be removed by Windows if its callback times out. Window queries, switching and persistence are queued outside the callback, but a stalled UI thread can still affect it; restart the app if it stops responding. The program does not claim universal suppression or switching support.

## Build and validate on Windows

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run:

```powershell
dotnet build SmartLanguageSwitcher -c Release -warnaserror
dotnet publish SmartLanguageSwitcher -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish
./scripts/Validate.ps1
```

The validation script runs the **packaged executable** with `--self-test <report.json>`. It checks all three chord state machines, repeat/up suppression, independent modifiers, AltGr exclusion, cycling, history, rejected requests, restart recovery, settings corruption, actual hook installation, settings-form initialization, and actual Win32 layout requests on a dedicated test window. On a runner with one layout it temporarily loads French for the integration check. It neither enables startup nor writes production settings. The test window briefly appears.

These automated checks do **not** simulate physical key delivery through the Windows shell, verify native flyout suppression, test every application, or validate visual appearance at every DPI. Before relying on a shortcut, manually test three or more layouts in your normal applications: first activation, repeated presses, modifier release, external switching, focus changes, and native hotkey conflicts. Check startup after sign-in and settings at your display scale.

GitHub Actions builds on Windows, validates the single-file EXE, generates a SHA-256 checksum, and uploads artifacts. A `v*` tag creates a GitHub Release **only after validation succeeds**. The release includes the executable, its checksum, and the actual validation report. Single-file native components extract into the runtime's temporary cache on first launch.

## Implementation

- `ChordState.cs`: deterministic keyboard state and suppression decisions.
- `KeyboardHook.cs`: low-level hook adapter; queues work on the UI thread.
- `LayoutHistory.cs`: deterministic cycle and confirmed recent-layout history.
- `SwitcherContext.cs`: foreground observation, confirmation, tray and session management.
- `Native.cs`: Windows interop.
- `Settings.cs`, `SettingsForm.cs`, `Startup.cs`: persistence, preferences and startup.
- `SelfTest.cs`, `scripts/Validate.ps1`: executable regression and Windows integration tests.

Windows API references: [WM_INPUTLANGCHANGEREQUEST](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-inputlangchangerequest), [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [GetKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardlayout).

## License

MIT; see [LICENSE](LICENSE).
