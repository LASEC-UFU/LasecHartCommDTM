# Run as Administrator
$src    = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\Release\net35"
$dtmDir = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM"
$dtmDll = "$dtmDir\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v2.0.50727\regasm.exe"

Write-Host "1. Copying DLL..."
Copy-Item "$src\LasecHartCommDTM.dll" "$dtmDir\Dtm\" -Force
Write-Host "   Done."

Write-Host "2. Unregistering old COM registration..."
$regasm4 = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"
& $regasm4 /u $dtmDll 2>&1 | Out-Null

Write-Host "3. Registering with CLR 2.0 regasm..."
& $regasm /codebase $dtmDll

Write-Host "4. Ensuring FDT category..."
$hkcr32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)
$catKey = $hkcr32.OpenSubKey("Component Categories\{036D1490-387B-11D4-86E1-00E0987270B9}")
if ($catKey -eq $null) {
    $catKey = $hkcr32.CreateSubKey("Component Categories\{036D1490-387B-11D4-86E1-00E0987270B9}")
    $catKey.SetValue("", "FDT DTM")
    $catKey.Close()
    Write-Host "   Created FDT category."
} else {
    Write-Host "   FDT category ok: $($catKey.GetValue(''))"
    $catKey.Close()
}
$hkcr32.Close()

Write-Host ""
Write-Host "Done. Run PACTware > Update Catalog."
