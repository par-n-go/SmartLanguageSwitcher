# v1.1.3

- Keep keyboard handling independent of the settings/UI thread.
- Renew the keyboard hook during idle periods to recover after Windows removes it.

Recovery may take up to 30 seconds plus a keyboard idle period. Win+Space remains best effort. The EXE remains unsigned.
