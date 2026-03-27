Write-Host "Registrando CWHart OCX..."
regsvr32 /s "C:\Windows\SysWOW64\CWHARTFDT.ocx"
$hr = $LASTEXITCODE
Write-Host "regsvr32 exit code: $hr"

# Verificar
$key = Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}" -ErrorAction SilentlyContinue
if ($key) { Write-Host "CWHart registrado OK" } else { Write-Host "FALHOU - CWHart nao registrado" }
