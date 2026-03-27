# Temporarily unregister our DTM to test if CWHart works without us
$dtmDll = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

Write-Host "1. Unregistering our DTM..."
& $regasm /u /tlb $dtmDll 2>&1

Write-Host ""
Write-Host "2. Removing our TypeLib registration..."
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

# Remove our TypeLib
$ourTlb = "TypeLib\{40498B38-0A79-3F68-905F-7954AA76AEA6}"
$k = $key32.OpenSubKey($ourTlb)
if ($k) {
    $k.Close()
    $key32.DeleteSubKeyTree($ourTlb)
    Write-Host "   Removed our TypeLib"
} else {
    Write-Host "   Our TypeLib already gone"
}

Write-Host ""
Write-Host "3. Removing our TLB file..."
$tlbFile = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\LasecHartCommDTM.tlb"
if (Test-Path $tlbFile) {
    Remove-Item $tlbFile -Force
    Write-Host "   TLB file removed"
}

Write-Host ""
Write-Host "4. Restoring ALL FDT interface names and TypeLibs..."
$jigfdtTlb = "{036D1471-387B-11D4-86E1-00E0987270B9}"
$jigfdtVer = "1.4e84"
$fdtInterfaces = @{
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
    "{036D147C-387B-11D4-86E1-00E0987270B9}" = "IDtmDocumentation"
    "{036D1485-387B-11D4-86E1-00E0987270B9}" = "IFdtCommunicationEvents"
    "{E4F31A10-45BF-11D4-BBB3-0060080993FF}" = "IFdtChannelCollection"
}
$fixCount = 0
foreach ($iid in $fdtInterfaces.Keys) {
    $correctName = $fdtInterfaces[$iid]
    $tlKey = $key32.OpenSubKey("Interface\$iid\TypeLib", $true)
    if ($tlKey) {
        $current = $tlKey.GetValue("")
        if ($current -ne $jigfdtTlb) {
            $tlKey.SetValue("", $jigfdtTlb)
            $tlKey.SetValue("Version", $jigfdtVer)
            $fixCount++
            Write-Host "   Fixed TypeLib $correctName"
        }
        $tlKey.Close()
    }
    $intKey = $key32.OpenSubKey("Interface\$iid", $true)
    if ($intKey) {
        $currentName = $intKey.GetValue("")
        if ($currentName -ne $correctName) {
            $intKey.SetValue("", $correctName)
            $fixCount++
            Write-Host "   Fixed Name $currentName -> $correctName"
        }
        $intKey.Close()
    }
}
Write-Host "   Fixed $fixCount entries"

Write-Host ""
Write-Host "5. Checking remaining interfaces pointing to our TypeLib..."
$intKey = $key32.OpenSubKey("Interface")
$remaining = 0
foreach ($iid in $intKey.GetSubKeyNames()) {
    $tlKey = $intKey.OpenSubKey("$iid\TypeLib")
    if ($tlKey) {
        $g = $tlKey.GetValue("")
        if ($g -eq "{40498B38-0A79-3F68-905F-7954AA76AEA6}") {
            $nameKey = $intKey.OpenSubKey($iid)
            Write-Host "   STILL: {$iid}  $($nameKey.GetValue(''))"
            $nameKey.Close()
            $remaining++
        }
        $tlKey.Close()
    }
}
$intKey.Close()
Write-Host "   $remaining interface(s) still reference our TypeLib"

$key32.Close()

Write-Host ""
Write-Host "Done. Our DTM is completely unregistered."
Write-Host "Close PACTware completely, reopen, and test if CWHart can be added."
Write-Host "Then run deploy_admin.ps1 to re-register our DTM."
