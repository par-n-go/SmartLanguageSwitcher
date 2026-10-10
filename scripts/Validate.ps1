param([string]$Exe = "$PSScriptRoot\..\publish\SmartLanguageSwitcher.exe", [string]$Report = "$PSScriptRoot\..\publish\validation.json")
$ErrorActionPreference = 'Stop'
$Exe = [IO.Path]::GetFullPath($Exe)
$Report = [IO.Path]::GetFullPath($Report)
$p = Start-Process -FilePath $Exe -ArgumentList @('--self-test', ('"' + $Report + '"')) -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(30000)) { $p.Kill(); throw 'Validation exceeded 30 seconds.' }
if ($p.ExitCode -ne 0) { if (Test-Path $Report) { Get-Content $Report }; throw "Validation failed: $($p.ExitCode)" }
$result = Get-Content $Report -Raw | ConvertFrom-Json
if ($result.exitCode -ne 0 -or $result.checks -lt 80) { throw 'Incomplete validation report.' }
$result
