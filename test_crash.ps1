# Temporarily remove our CLSID from FDT category and test
# Run as Administrator

param([switch]$Remove, [switch]$Restore)

$clsid = "{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}"
$fdtCat = "{036D1490-387B-11D4-86E1-00E0987270B9}"
$hkcr32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey('ClassesRoot', [Microsoft.Win32.RegistryView]::Registry32)

if ($Remove) {
    Write-Host "Removing our CLSID from FDT category..."
    $key = $hkcr32.OpenSubKey("CLSID\$clsid\Implemented Categories", $true)
    if ($key) {
        $key.DeleteSubKey($fdtCat, $false)
        $key.Close()
        Write-Host "Done. Now run PACTware Update Catalog and see if it crashes."
    } else {
        Write-Host "Key not found!"
    }
}
elseif ($Restore) {
    Write-Host "Restoring our CLSID to FDT category..."
    $key = $hkcr32.CreateSubKey("CLSID\$clsid\Implemented Categories\$fdtCat")
    $key.Close()
    Write-Host "Done."
}
else {
    # Show current state
    $ic = $hkcr32.OpenSubKey("CLSID\$clsid\Implemented Categories")
    if ($ic) {
        Write-Host "Implemented Categories for $clsid :"
        foreach ($sub in $ic.GetSubKeyNames()) { Write-Host "  $sub" }
        $ic.Close()
    }
}
$hkcr32.Close()
