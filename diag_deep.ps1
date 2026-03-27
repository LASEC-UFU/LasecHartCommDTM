# Deep diagnostic: find ALL interfaces that point to our TypeLib
$ourTlb = "{40498B38-0A79-3F68-905F-7954AA76AEA6}"
$jigfdtTlb = "{036D1471-387B-11D4-86E1-00E0987270B9}"
$cwTlb = "{6A4CF9E6-7C73-4E3A-B8A2-F3515387CC59}"

$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

# 1. Check all interfaces pointing to our TypeLib
Write-Host "=== Interfaces pointing to OUR TypeLib ($ourTlb) ==="
$intKey = $key32.OpenSubKey("Interface")
$count = 0
foreach ($iid in $intKey.GetSubKeyNames()) {
    $tlKey = $intKey.OpenSubKey("$iid\TypeLib")
    if ($tlKey) {
        $g = $tlKey.GetValue("")
        if ($g -eq $ourTlb) {
            $nameKey = $intKey.OpenSubKey($iid)
            $name = $nameKey.GetValue("")
            Write-Host "  {$iid}  $name  TypeLib=$g  Ver=$($tlKey.GetValue('Version'))"
            $nameKey.Close()
            $count++
        }
        $tlKey.Close()
    }
}
Write-Host "Total: $count interface(s) pointing to our TypeLib"
$intKey.Close()

# 2. Check CWHart CLSID
Write-Host ""
Write-Host "=== CWHart CLSID ==="
$cw = $key32.OpenSubKey("CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}")
if ($cw) {
    Write-Host "CWHart CLSID: EXISTS"
    $ip = $cw.OpenSubKey("InprocServer32")
    if ($ip) {
        Write-Host "  InprocServer32: $($ip.GetValue(''))"
        Write-Host "  ThreadingModel: $($ip.GetValue('ThreadingModel'))"
        $ip.Close()
    }
    $tl = $cw.OpenSubKey("TypeLib")
    if ($tl) {
        Write-Host "  TypeLib: $($tl.GetValue(''))"
        $tl.Close()
    }
    $cw.Close()
} else {
    Write-Host "CWHart CLSID: NOT FOUND!"
}

# 3. Check Jigfdt TypeLib registration
Write-Host ""
Write-Host "=== Jigfdt TypeLib ==="
$jig = $key32.OpenSubKey("TypeLib\$jigfdtTlb")
if ($jig) {
    Write-Host "Jigfdt TypeLib: EXISTS"
    foreach ($ver in $jig.GetSubKeyNames()) {
        Write-Host "  Version: $ver"
        $verKey = $jig.OpenSubKey($ver)
        foreach ($sub in $verKey.GetSubKeyNames()) {
            $subKey = $verKey.OpenSubKey($sub)
            if ($sub -eq "FLAGS") {
                Write-Host "    FLAGS: $($subKey.GetValue(''))"
            } elseif ($sub -eq "HELPDIR") {
                Write-Host "    HELPDIR: $($subKey.GetValue(''))"
            } else {
                # LCID like 0
                foreach ($plat in $subKey.GetSubKeyNames()) {
                    $platKey = $subKey.OpenSubKey($plat)
                    Write-Host "    $sub\$plat : $($platKey.GetValue(''))"
                    $platKey.Close()
                }
            }
            $subKey.Close()
        }
        $verKey.Close()
    }
    $jig.Close()
} else {
    Write-Host "Jigfdt TypeLib: NOT FOUND!"
}

# 4. Check our TypeLib registration
Write-Host ""
Write-Host "=== Our TypeLib ==="
$our = $key32.OpenSubKey("TypeLib\$ourTlb")
if ($our) {
    Write-Host "Our TypeLib: EXISTS"
    foreach ($ver in $our.GetSubKeyNames()) {
        Write-Host "  Version: $ver"
        $verKey = $our.OpenSubKey($ver)
        foreach ($sub in $verKey.GetSubKeyNames()) {
            $subKey = $verKey.OpenSubKey($sub)
            if ($sub -eq "FLAGS") {
                Write-Host "    FLAGS: $($subKey.GetValue(''))"
            } elseif ($sub -eq "HELPDIR") {
                Write-Host "    HELPDIR: $($subKey.GetValue(''))"
            } else {
                foreach ($plat in $subKey.GetSubKeyNames()) {
                    $platKey = $subKey.OpenSubKey($plat)
                    Write-Host "    $sub\$plat : $($platKey.GetValue(''))"
                    $platKey.Close()
                }
            }
            $subKey.Close()
        }
        $verKey.Close()
    }
    $our.Close()
} else {
    Write-Host "Our TypeLib: NOT FOUND"
}

# 5. Check CWHart TypeLib
Write-Host ""
Write-Host "=== CWHart TypeLib ==="
$cwt = $key32.OpenSubKey("TypeLib\$cwTlb")
if ($cwt) {
    Write-Host "CWHart TypeLib: EXISTS"
    foreach ($ver in $cwt.GetSubKeyNames()) {
        Write-Host "  Version: $ver"
        $verKey = $cwt.OpenSubKey($ver)
        foreach ($sub in $verKey.GetSubKeyNames()) {
            $subKey = $verKey.OpenSubKey($sub)
            if ($sub -eq "FLAGS") {
                Write-Host "    FLAGS: $($subKey.GetValue(''))"
            } elseif ($sub -eq "HELPDIR") {
                Write-Host "    HELPDIR: $($subKey.GetValue(''))"
            } else {
                foreach ($plat in $subKey.GetSubKeyNames()) {
                    $platKey = $subKey.OpenSubKey($plat)
                    Write-Host "    $sub\$plat : $($platKey.GetValue(''))"
                    $platKey.Close()
                }
            }
            $subKey.Close()
        }
        $verKey.Close()
    }
    $cwt.Close()
} else {
    Write-Host "CWHart TypeLib: NOT FOUND!"
}

# 6. Check CWHart Implemented Categories
Write-Host ""
Write-Host "=== CWHart Implemented Categories ==="
$cwCat = $key32.OpenSubKey("CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}\Implemented Categories")
if ($cwCat) {
    foreach ($cat in $cwCat.GetSubKeyNames()) { Write-Host "  $cat" }
    $cwCat.Close()
} else {
    Write-Host "  NOT FOUND"
}

# 7. Check if CWHart file exists
Write-Host ""
Write-Host "=== CWHart File ==="
$cwFile = "C:\Windows\SysWow64\CWHARTFDT.ocx"
if (Test-Path $cwFile) {
    $f = Get-Item $cwFile
    Write-Host "  $cwFile  Size=$($f.Length)  Modified=$($f.LastWriteTime)"
} else {
    Write-Host "  CWHARTFDT.ocx NOT FOUND!"
}

$key32.Close()
