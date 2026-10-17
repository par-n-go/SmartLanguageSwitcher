# v1.1.6

- Reset unfinished shortcuts when the Windows session locks or disconnects.
- Renew the keyboard hook after unlock/reconnect once keys are released.
- Record session transitions in optional diagnostics.

Automated tests simulate session notifications; physical lock/unlock still needs verification. Win+Space remains best effort. The EXE is unsigned.
