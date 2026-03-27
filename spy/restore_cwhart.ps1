# restore_cwhart.ps1 — Run as Administrator
# Restores CWHart's original COM registration (undoes deploy_spy.ps1)

$ErrorActionPreference = "Stop"

$cwClsid    = "{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}"
$backupFile = "C:\Temp\cwhart_backup.txt"

if (-not (Test-Path $backupFile)) {
    Write-Host "ERROR: Backup file not found at $backupFile" -ForegroundColor Red
    Write-Host "Cannot restore without backup. Manual fix:"
    Write-Host "  Set HKCR\CLSID\$cwClsid\InprocServer32 (Default) = C:\Windows\SysWow64\CWHARTFDT.ocx"
    Write-Host "  Delete Assembly, Class, RuntimeVersion, CodeBase values"
    exit 1
}

Write-Host "=== Restoring CWHart Original Registration ===" -ForegroundColor Cyan

# Parse backup file
$backup = @{}
Get-Content $backupFile | ForEach-Object {
    if ($_ -match '^(\w+)=(.*)$') { $backup[$Matches[1]] = $Matches[2] }
}

$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)
$ipsKey = $key32.OpenSubKey("CLSID\$cwClsid\InprocServer32", $true)
if (-not $ipsKey) {
    Write-Host "ERROR: CWHart CLSID InprocServer32 key not found!" -ForegroundColor Red
    exit 1
}

# Restore original InprocServer32
$origServer = $backup["InprocServer32"]
if ([string]::IsNullOrEmpty($origServer)) {
    $origServer = "C:\Windows\SysWow64\CWHARTFDT.ocx"
}
$ipsKey.SetValue("", $origServer)

$origThread = $backup["ThreadingModel"]
if ([string]::IsNullOrEmpty($origThread)) { $origThread = "Apartment" }
$ipsKey.SetValue("ThreadingModel", $origThread)

# Remove .NET-specific values added by the spy
foreach ($valName in @("Assembly", "Class", "RuntimeVersion", "CodeBase")) {
    try { $ipsKey.DeleteValue($valName, $false) } catch { }
}

$ipsKey.Close()
$key32.Close()

Write-Host "Restored InprocServer32 = $origServer" -ForegroundColor Green
Write-Host "Restored ThreadingModel = $origThread"
Write-Host "Removed Assembly, Class, RuntimeVersion, CodeBase"
Write-Host ""
Write-Host "=== CWHart restored to original ===" -ForegroundColor Green
Write-Host "You can now use CWHart normally in PACTware."
