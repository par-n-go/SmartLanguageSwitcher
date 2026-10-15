# Technical details

## Shortcuts

Hold Alt, Ctrl or Win before pressing Shift or Space. First press selects the previous layout; further presses visit other installed layouts, then the starting layout. Auto-repeat does not cycle. The last observed layout is committed on modifier release. With no valid history, the first other installed layout is used.

Left/right modifiers are tracked separately. Observed modifiers are checked against Windows state to recover missed releases; synthetic releases also clear them. Snapshots never arm a new chord. Queued requests are discarded after a focus change, pause, settings change or 250 ms delay. Ctrl+Alt chords are excluded for AltGr. Injected keystrokes do not activate switching. Win+Space sends an unassigned VK_E8 down/up pair when consumed so Windows does not mistake the gesture for Win alone. Win-up is still delivered to avoid a stuck modifier. This relies on shell behavior and remains best effort, especially across integrity levels and other hooks. Alt may still activate an application menu.

To disable native Alt+Shift/Ctrl+Shift: Settings → Time & language → Typing → Advanced keyboard settings → Input language hot keys → Change Key Sequence → Not Assigned. Check per-language shortcuts too. Win+Space's shell flyout cannot be universally suppressed.

## History and persistence

The foreground thread's HKL is sampled every 100 ms. External changes and application focus changes update a global recent-layout pair. Intermediate session selections are excluded. Requests have 800 ms to be confirmed; failures follow the actual observed layout. Fast changes between samples may be missed.

Settings are atomically replaced in `%APPDATA%\SmartLanguageSwitcher\settings.json`. Corrupt settings fall back to defaults; removed layouts are ignored. HKLs are not stable identifiers for every IME/profile across restarts. TSF/IME modes, elevated apps, games, remote sessions and secure desktop are not universally supported. The hook runs on a dedicated message-loop thread, independent of UI and settings I/O. Because Windows provides no notification when it silently removes a timed-out hook, the app renews it every 30 seconds once no shortcut is active, keys are released and keyboard input has been idle for 500 ms. Failed installation is retried after five seconds. Recovery is deferred while typing; elevated or secure desktops can still prevent input access.

Startup uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SmartLanguageSwitcher`. Windows Startup Apps can separately disable it. To uninstall: disable startup, exit, delete the EXE and optionally the settings folder.

## Validation and releases

`--self-test <report.json>` runs deterministic chord/history/persistence tests, hook installation and recovery after forced removal, input delivery while the caller is not pumping messages, icon/settings initialization, and real Win32 layout requests against a test window. `scripts/Validate.ps1` executes the packaged EXE with a timeout. A runner with one layout temporarily loads French. Tests do not enable startup or write production settings.

Physical Win+Space interaction with the shell requires manual testing; simulated input did not reliably reproduce the reported Start-menu behavior. Test Win alone, both Win keys with Space, repeated Space presses, external layout changes and startup after sign-in. The settings UI can be inspected without saving with `--preview-settings`.

GitHub Actions validates the self-contained x64 EXE before uploading it. A `v*` tag publishes the EXE, SHA-256 checksum and validation report only after success. The runtime extracts bundled native components into a temporary cache. The icon includes nine sizes from 16 to 256 pixels; regenerate with `scripts/Generate-Icon.ps1`.

## Download warnings

A warning can come from browser reputation, SmartScreen, Smart App Control or antivirus. Its exact wording matters. This project currently has no trusted code-signing identity. Distribution signing requires a verified publisher and a trusted certificate/service; self-signing is not a SmartScreen fix. Even a new trusted signature may need reputation. Do not disable Windows security to distribute the app. If antivirus identifies a specific threat, investigate it separately.

References: [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), [layout requests](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-inputlangchangerequest), [menu masking](https://github.com/AutoHotkey/AutoHotkeyDocs/blob/v2/docs/lib/A_MenuMaskKey.htm), [SmartScreen](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Automatic updates

Enabled by default. Checks the latest stable GitHub release 30 seconds after launch and every six hours; the tray menu also offers a manual check. Older versions need one manual upgrade to v1.1.0 to gain the updater.

The updater downloads only this repository's published EXE, checks its size, version, SHA-256 checksum and GitHub asset digest when available, then waits for shortcuts and settings to be idle. A temporary helper replaces the EXE at its existing path and restarts it. Settings and startup paths are preserved. If the new process does not acknowledge startup, the helper attempts rollback and restarts the previous EXE. A failed version is skipped by automatic checks; manual checks can retry it.

The EXE folder must be writable. Offline access, GitHub rate limits, file locks or Windows policies can prevent an update; the current app remains available and later checks retry. Downloads/cache and the most recent error are under `%LOCALAPPDATA%\SmartLanguageSwitcher\updates`. Old helper files are cleaned on a later launch. SHA-256 verification checks integrity against GitHub; it is not publisher code signing, and unsigned-app restrictions still apply.

The packaged validation script also tests actual replacement, restart and rollback using disposable copies. It does not modify the installed app or production settings.

## Image sources

English SmartScreen illustrations by ToBeFree via Wikimedia Commons: [More info](https://commons.wikimedia.org/wiki/File:Smartscreen-warning-1-arrow.svg) and [Run anyway](https://commons.wikimedia.org/wiki/File:Smartscreen-warning-2-arrow.svg). Public domain; arrows changed from green to red. These are vector recreations of the Windows 10 prompts, not screenshots of this app. Windows 11 appearance may differ.

## Optional diagnostics

Disabled by default. To enable temporarily, put an ISO 8601 UTC expiry time in `%LOCALAPPDATA%\SmartLanguageSwitcher\diagnostics\enabled-until.txt`, then restart the app. Delete the file and restart to disable early. Logs contain modifier/shortcut events, layout identifiers and handler/UI status, never typed text or window titles. Writes run outside the keyboard hook. `shortcuts.log` rotates after 128 KiB with one previous file; a bounded queue limits pending entries. Logging stops at the expiry time. Diagnostic files stay local and are not uploaded.
