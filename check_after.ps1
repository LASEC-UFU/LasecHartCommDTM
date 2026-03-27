$k32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$our = $k32.OpenSubKey("CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}")
if ($our) { Write-Host "OurDTM = STILL REGISTERED"; $our.Close() }
else { Write-Host "OurDTM = GONE (clean)" }

$tl = $k32.OpenSubKey("TypeLib\{40498B38-0A79-3F68-905F-7954AA76AEA6}")
if ($tl) { Write-Host "OurTLB = STILL REGISTERED"; $tl.Close() }
else { Write-Host "OurTLB = GONE (clean)" }

$cw = $k32.OpenSubKey("CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}")
if ($cw) { Write-Host "CWHart = REGISTERED OK"; $cw.Close() }
else { Write-Host "CWHart = MISSING!" }

# Save fresh interfaces
$outDir = "C:\SourceCode\LasecHartCommDTM\pactware_snapshot_after"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
reg export "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}" "$outDir\cwhart_clsid.reg" /y 2>$null
reg export "HKLM\SOFTWARE\WOW6432Node\Classes\TypeLib\{036D1471-387B-11D4-86E1-00E0987270B9}" "$outDir\jigfdt_typelib.reg" /y 2>$null

$iids = @{
    "{036D147F-387B-11D4-86E1-00E0987270B9}" = "IDtmInformation"
    "{036D1481-387B-11D4-86E1-00E0987270B9}" = "IDtm"
    "{039ECFC4-9CA8-44E6-944D-B37F288A34D8}" = "IFdtCommunication"
    "{036D147D-387B-11D4-86E1-00E0987270B9}" = "IDtmParameter"
    "{036D1480-387B-11D4-86E1-00E0987270B9}" = "IDtmActiveXInfo"
    "{036D1486-387B-11D4-86E1-00E0987270B9}" = "IDtmActiveXCtrl"
    "{036D1478-387B-11D4-86E1-00E0987270B9}" = "IFdtEvents"
    "{036D1484-387B-11D4-86E1-00E0987270B9}" = "IFdtChannelSubTopo"
    "{036D1489-387B-11D4-86E1-00E0987270B9}" = "IDtmChannel"
    "{036D1488-387B-11D4-86E1-00E0987270B9}" = "IFdtChannel"
    "{F15BA42E-BBF1-42ED-8009-7F664A002CFB}" = "IDtmEventsSource"
}

Write-Host ""
Write-Host "=== Interface TypeLib Mapping (Fresh Install) ==="
$lines = @()
foreach ($iid in ($iids.Keys | Sort-Object)) {
    $name = $iids[$iid]
    $ik = $k32.OpenSubKey("Interface\$iid")
    if ($ik) {
        $tlib = $ik.OpenSubKey("TypeLib")
        $tlv = if ($tlib) { $tlib.GetValue("") } else { "NONE" }
        $line = "$name -> $tlv"
        Write-Host "  $line"
        $lines += $line
        if ($tlib) { $tlib.Close() }
        $ik.Close()
    } else {
        $line = "$name -> NOT REGISTERED"
        Write-Host "  $line"
        $lines += $line
    }
}
$lines | Out-File "$outDir\interfaces_fresh.txt" -Encoding UTF8

$k32.Close()
Write-Host "`nDone. Saved to $outDir"
