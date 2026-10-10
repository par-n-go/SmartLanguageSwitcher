# Smart Language Switcher v1.0.0

Windows 11 x64 tray utility with previous-layout switching, held-modifier cycling, confirmed layout history, foreground tracking, settings and per-user startup.

Fixes include suppression of repeated trigger key-downs, independent left/right modifier tracking, confirmation before history updates, external-change tracking without a blind delay, atomic settings writes and invalid-setting recovery.

The self-contained single-file EXE is built and executed on Windows before publication. See the attached validation report and SHA-256 checksum. No separate .NET installation is required.

## Known limitations

- Disable Windows' matching Alt+Shift / Ctrl+Shift shortcut to avoid native conflicts.
- Win+Space is best effort: Windows may retain its flyout and native behavior.
- Physical hotkey interaction with the shell is not covered by the automated checks.
- Layout requests can be rejected. Elevated apps, TSF/IME profiles, games, remote sessions and secure desktop are not universally supported.
- The executable is unsigned; single-file native components extract to a temporary runtime cache.

See README for installation, verification, configuration and the manual acceptance checklist.
