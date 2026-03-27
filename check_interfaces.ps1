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

$k32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$outFile = "C:\SourceCode\LasecHartCommDTM\pactware_snapshot_before\interfaces.txt"
$lines = @()

foreach ($iid in $iids.Keys) {
    $name = $iids[$iid]
    $ik = $k32.OpenSubKey("Interface\$iid")
    if ($ik) {
        $desc = $ik.GetValue("")
        $ps = $ik.OpenSubKey("ProxyStubClsid32")
        $psv = if ($ps) { $ps.GetValue("") } else { "NONE" }
        $tl = $ik.OpenSubKey("TypeLib")
        $tlv = if ($tl) { $tl.GetValue("") } else { "NONE" }
        $tv = if ($tl) { $tl.GetValue("Version") } else { "?" }
        $line = "$name $iid : desc=$desc proxy=$psv typelib=$tlv ver=$tv"
        Write-Host $line
        $lines += $line
        if ($ps) { $ps.Close() }
        if ($tl) { $tl.Close() }
        $ik.Close()
    } else {
        $line = "$name $iid : NOT REGISTERED"
        Write-Host $line
        $lines += $line
    }
}

$k32.Close()
$lines | Out-File $outFile -Encoding UTF8
Write-Host "`nSaved to $outFile"
