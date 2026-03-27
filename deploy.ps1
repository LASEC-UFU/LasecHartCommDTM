# Execute como Administrador
$dtmDir = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM"
$dtmDll = "$dtmDir\Dtm\LasecHartCommDTM.dll"
$src    = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\x86\Release\net48"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

Write-Host "1. Copiando DLLs..."
New-Item -ItemType Directory -Force -Path "$dtmDir\Dtm" | Out-Null
Copy-Item "$src\LasecHartCommDTM.dll" "$dtmDir\Dtm\" -Force

Write-Host "2. Re-registrando COM (32-bit)..."
& $regasm /codebase $dtmDll

Write-Host "3. Verificando Component Categories no hive 32-bit..."
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$catKey = $key32.OpenSubKey("Component Categories\{036D1490-387B-11D4-86E1-00E0987270B9}")
if ($catKey -eq $null) {
    Write-Host "   Criando Component Categories no hive 32-bit..."
    $catKey = $key32.CreateSubKey("Component Categories\{036D1490-387B-11D4-86E1-00E0987270B9}")
    $catKey.SetValue("", "FDT DTM")
    $catKey.Close()
} else {
    Write-Host "   Component Categories ja existe: $($catKey.GetValue(''))"
    $catKey.Close()
}
$key32.Close()

Write-Host ""
Write-Host "Feito! Atualize o catalogo no PACTware agora."
