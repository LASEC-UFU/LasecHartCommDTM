$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$names = @{
    "{036D147F-387B-11D4-86E1-00E0987270B9}" = "IDtmInformation"
    "{036D1481-387B-11D4-86E1-00E0987270B9}" = "IDtm"
    "{039ECFC4-9CA8-44E6-944D-B37F288A34D8}" = "IFdtCommunication"
    "{036D147D-387B-11D4-86E1-00E0987270B9}" = "IDtmParameter"
    "{036D1480-387B-11D4-86E1-00E0987270B9}" = "IDtmActiveXInformation"
    "{036D1486-387B-11D4-86E1-00E0987270B9}" = "IDtmActiveXControl"
    "{036D1478-387B-11D4-86E1-00E0987270B9}" = "IFdtEvents"
    "{036D1484-387B-11D4-86E1-00E0987270B9}" = "IFdtChannelSubTopology"
    "{036D1489-387B-11D4-86E1-00E0987270B9}" = "IDtmChannel"
    "{036D1488-387B-11D4-86E1-00E0987270B9}" = "IFdtChannel"
    "{F15BA42E-BBF1-42ED-8009-7F664A002CFB}" = "IDtmEventsSource"
}

$jigfdt = "{036D1471-387B-11D4-86E1-00E0987270B9}"

foreach ($iid in $names.Keys) {
    $name = $names[$iid]
    $tlKey = $key32.OpenSubKey("Interface\$iid\TypeLib", $false)
    if ($tlKey) {
        $g = $tlKey.GetValue("")
        $v = $tlKey.GetValue("Version")
        if ($g -eq $jigfdt) {
            Write-Host "OK     $name  TypeLib=$g  Ver=$v"
        } else {
            Write-Host "WRONG  $name  TypeLib=$g  Ver=$v  (expected $jigfdt)"
        }
        $tlKey.Close()
    } else {
        Write-Host "MISS   $name  No TypeLib key found"
    }
}
$key32.Close()

# Also check our DTM CLSID exists
Write-Host ""
$dtmClsid = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}"
if (Test-Path $dtmClsid) {
    Write-Host "Our DTM CLSID: REGISTERED"
} else {
    Write-Host "Our DTM CLSID: NOT FOUND"
}

$ctrlClsid = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}"
if (Test-Path $ctrlClsid) {
    Write-Host "ConfigControl CLSID: REGISTERED"
} else {
    Write-Host "ConfigControl CLSID: NOT FOUND"
}
