# Run as Administrator
$src    = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\x86\Release\net48"
$dtmDir = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM"
$dtmDll = "$dtmDir\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

Write-Host "1. Copying DLL from $src..."
New-Item -ItemType Directory -Force -Path "$dtmDir\Dtm" | Out-Null
Copy-Item "$src\LasecHartCommDTM.dll" "$dtmDir\Dtm\" -Force
Write-Host "   Done: $(Get-Item $dtmDll | Select-Object -ExpandProperty LastWriteTime)"

Write-Host "2. Unregistering old COM registration..."
& $regasm /u $dtmDll 2>&1 | Out-Null
$regasm2 = "C:\Windows\Microsoft.NET\Framework\v2.0.50727\regasm.exe"
if (Test-Path $regasm2) { & $regasm2 /u $dtmDll 2>&1 | Out-Null }

Write-Host "3. Registering with CLR 4.0 regasm (x86) + TypeLib..."
# /codebase = registers CodeBase pointing to assembly on disk
# /tlb      = generates and registers type library (.tlb) for native COM callers
# [ComRegisterFunction] adds: Implemented Categories, TypeLib, Programmable, VERSION
& $regasm /codebase /tlb $dtmDll

Write-Host "4. Clearing log..."
$logFile = "C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log"
[System.IO.File]::WriteAllText($logFile, "", [System.Text.Encoding]::UTF8)

Write-Host "5. Verificando registro completo..."
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}" /s 2>&1

Write-Host ""
Write-Host "Done. Run PACTware > Update Catalog."
Write-Host "Then check: type `"$logFile`""
