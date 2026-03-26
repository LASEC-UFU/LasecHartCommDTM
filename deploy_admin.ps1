# Run as Administrator
# Deploy Lasec HART Communication DTM to PACTware
# Usage: Right-click > Run as Administrator
#   or: Start-Process powershell -Verb RunAs -ArgumentList "-File deploy_admin.ps1"

# Source: worktree build output (or main repo)
$worktreeSrc = "C:\SourceCode\LasecHartCommDTM\.claude\worktrees\happy-albattani\src\LasecHartCommDTM\bin\x86\Release\net48"
$mainSrc     = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\x86\Release\net48"

# Use worktree build if available, otherwise main repo
if (Test-Path "$worktreeSrc\LasecHartCommDTM.dll") {
    $src = $worktreeSrc
} else {
    $src = $mainSrc
}

$dtmDir = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM"
$dtmDll = "$dtmDir\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"
$logFile = "C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Lasec HART Communication DTM - Deploy" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source: $src" -ForegroundColor Yellow
Write-Host "Target: $dtmDir" -ForegroundColor Yellow
Write-Host ""

# 1. Copy DLL
Write-Host "1. Copying DLL..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path "$dtmDir\Dtm" | Out-Null
Copy-Item "$src\LasecHartCommDTM.dll" "$dtmDir\Dtm\" -Force
Write-Host "   Done: $(Get-Item $dtmDll | Select-Object -ExpandProperty LastWriteTime)"

# 2. Unregister old
Write-Host "2. Unregistering old COM registration..." -ForegroundColor Green
& $regasm /u $dtmDll 2>&1 | Out-Null
$regasm2 = "C:\Windows\Microsoft.NET\Framework\v2.0.50727\regasm.exe"
if (Test-Path $regasm2) { & $regasm2 /u $dtmDll 2>&1 | Out-Null }

# 3. Register new
Write-Host "3. Registering with CLR 4.0 regasm (x86) + TypeLib..." -ForegroundColor Green
& $regasm /codebase /tlb $dtmDll

# 4. Clear log
Write-Host "4. Clearing log..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path (Split-Path $logFile) | Out-Null
[System.IO.File]::WriteAllText($logFile, "", [System.Text.Encoding]::UTF8)

# 5. Verify registration
Write-Host ""
Write-Host "5. Verificando registro COM..." -ForegroundColor Green
Write-Host "--- CommDtm CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}" /v "" 2>&1
Write-Host "--- ConfigControl CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}" /v "" 2>&1
Write-Host "--- LogControl CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20040}" /v "" 2>&1
Write-Host "--- DeviceAddressControl CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20050}" /v "" 2>&1
Write-Host "--- DtmAddressControl CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20060}" /v "" 2>&1
Write-Host "--- AboutControl CLSID ---"
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20070}" /v "" 2>&1

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Deploy completo!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Proximos passos:" -ForegroundColor Yellow
Write-Host "  1. Abra PACTware 5.0"
Write-Host "  2. Menu: Extras > Update DTM Catalog"
Write-Host "  3. Adicione o 'Lasec HART Communication DTM' na arvore"
Write-Host "  4. Clique com botao direito > 'Parameter' (Configuracao)"
Write-Host ""
Write-Host "Log: $logFile" -ForegroundColor Yellow
Write-Host ""
pause
