# deploy_spy.ps1 — Run as Administrator
# Redirects CWHart's CLSID to our spy proxy DLL.
# Backs up original values so restore_cwhart.ps1 can undo everything.

$ErrorActionPreference = "Stop"

$cwClsid   = "{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}"
$spySrc    = "C:\SourceCode\LasecHartCommDTM\spy\bin\x86\Release\net48"
$spyDll    = "$spySrc\CWHartSpy.dll"
$backupFile = "C:\Temp\cwhart_backup.txt"
$logFile    = "C:\Temp\CWHartSpy.log"

if (-not (Test-Path $spyDll)) {
    Write-Host "ERROR: Spy DLL not found at $spyDll" -ForegroundColor Red
    Write-Host "Build first: dotnet build spy\CWHartSpy.csproj -c Release -p:Platform=x86"
    exit 1
}

Write-Host "=== CWHart Spy Deployment ===" -ForegroundColor Cyan

# 1. Open the CLSID's InprocServer32 key (32-bit registry)
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)
$ipsKey = $key32.OpenSubKey("CLSID\$cwClsid\InprocServer32", $true)
if (-not $ipsKey) {
    Write-Host "ERROR: CWHart CLSID not found in registry!" -ForegroundColor Red
    exit 1
}

# 2. Backup current values
$origServer   = $ipsKey.GetValue("")
$origThread   = $ipsKey.GetValue("ThreadingModel")
$origAssembly = $ipsKey.GetValue("Assembly")
$origClass    = $ipsKey.GetValue("Class")
$origRuntime  = $ipsKey.GetValue("RuntimeVersion")
$origCodeBase = $ipsKey.GetValue("CodeBase")

Write-Host "1. Backing up current CWHart registration..."
Write-Host "   InprocServer32 = $origServer"
Write-Host "   ThreadingModel = $origThread"

# Save backup
@"
InprocServer32=$origServer
ThreadingModel=$origThread
Assembly=$origAssembly
Class=$origClass
RuntimeVersion=$origRuntime
CodeBase=$origCodeBase
"@ | Set-Content $backupFile -Encoding UTF8
Write-Host "   Saved to $backupFile"

# 3. Redirect to our spy DLL (via .NET CLR hosting)
Write-Host "2. Redirecting CWHart CLSID to spy proxy..."

$ipsKey.SetValue("", "mscoree.dll")
$ipsKey.SetValue("ThreadingModel", "Both")
$ipsKey.SetValue("Assembly", "CWHartSpy, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
$ipsKey.SetValue("Class", "CWHartSpy.CWHartProxy")
$ipsKey.SetValue("RuntimeVersion", "v4.0.30319")
$ipsKey.SetValue("CodeBase", "file:///$($spyDll.Replace('\','/'))")

$ipsKey.Close()
$key32.Close()

Write-Host "   Done: {6358CCBF} now points to CWHartSpy.dll" -ForegroundColor Green

# 4. Clear spy log
Write-Host "3. Clearing spy log..."
[System.IO.File]::WriteAllText($logFile, "", [System.Text.Encoding]::UTF8)
Write-Host "   Log: $logFile"

Write-Host ""
Write-Host "=== SPY IS ACTIVE ===" -ForegroundColor Yellow
Write-Host "Now start PACTware, operate CWHart, and check the log:"
Write-Host "  type `"$logFile`""
Write-Host ""
Write-Host "To restore CWHart, run: restore_cwhart.ps1"
