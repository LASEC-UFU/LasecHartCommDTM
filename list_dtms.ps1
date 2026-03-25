$hkcr32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey('ClassesRoot', [Microsoft.Win32.RegistryView]::Registry32)
$clsidRoot = $hkcr32.OpenSubKey('CLSID')
$fdtCat = '{036D1490-387B-11D4-86E1-00E0987270B9}'
$count = 0
foreach ($clsid in $clsidRoot.GetSubKeyNames()) {
    $ic = $hkcr32.OpenSubKey("CLSID\$clsid\Implemented Categories\$fdtCat")
    if ($ic -ne $null) {
        $n = $hkcr32.OpenSubKey("CLSID\$clsid"); $name = $n.GetValue('')
        $ip = $hkcr32.OpenSubKey("CLSID\$clsid\InprocServer32")
        $dll = if ($ip) { $ip.GetValue('') } else { 'N/A' }
        $asm = if ($ip) { $ip.GetValue('Assembly') } else { '' }
        $cb  = if ($ip) { $ip.GetValue('CodeBase') } else { '' }
        Write-Host ("{$clsid} $name")
        Write-Host "  DLL: $dll"
        if ($asm) { Write-Host "  Assembly: $asm" }
        if ($cb)  { Write-Host "  CodeBase: $cb" }
        $ic.Close(); $n.Close(); if ($ip) { $ip.Close() }
        $count++
    }
}
Write-Host "`nTotal: $count FDT DTMs registered"
$hkcr32.Close()
