# Smart Language Switcher

A small Windows 11 tray app. Switch to your previous keyboard layout, or hold the modifier and tap again to cycle.

**[Download EXE](https://github.com/par-n-go/SmartLanguageSwitcher/releases/latest/download/SmartLanguageSwitcher.exe)** · [Release notes](https://github.com/par-n-go/SmartLanguageSwitcher/releases/latest)

- Choose **Alt+Shift**, **Ctrl+Shift**, or **Win+Space**.
- Release the modifier to confirm. Double-click the tray icon for settings.
- Put the EXE in a permanent folder before enabling **Start with Windows**.

**Limits:** Disable the matching Windows Alt+Shift/Ctrl+Shift shortcut. Win+Space is best effort. Some elevated apps and IMEs may reject switching.

**Download warning?** The EXE is unsigned. Trusted code signing requires a verified publisher; signing alone does not guarantee immediate SmartScreen reputation. [Microsoft guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)

### Build

Requires Windows and .NET 10 SDK.

```powershell
dotnet publish SmartLanguageSwitcher -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
./scripts/Validate.ps1
```

[Technical details](docs/DETAILS.md) · [MIT license](LICENSE)
