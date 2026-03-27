# Check interface NAME and ProxyStub entries for all FDT interfaces
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$interfaces = @{
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

Write-Host "=== FDT Interface Details (Name, TypeLib, ProxyStub, NumMethods) ==="
foreach ($iid in $interfaces.Keys) {
    $expected = $interfaces[$iid]
    $intKey = $key32.OpenSubKey("Interface\$iid")
    if ($intKey) {
        $actualName = $intKey.GetValue("")
        $nameOk = if ($actualName -eq $expected) { "OK" } else { "RENAMED!" }
        
        # TypeLib
        $tlKey = $intKey.OpenSubKey("TypeLib")
        $tlGuid = if ($tlKey) { $tlKey.GetValue(""); $tlKey.Close() } else { "NONE" }
        
        # ProxyStubClsid32
        $psKey = $intKey.OpenSubKey("ProxyStubClsid32")
        $ps = if ($psKey) { $psKey.GetValue(""); $psKey.Close() } else { "NONE" }
        
        # NumMethods
        $nmKey = $intKey.OpenSubKey("NumMethods")
        $nm = if ($nmKey) { $nmKey.GetValue(""); $nmKey.Close() } else { "NONE" }
        
        Write-Host "$nameOk  $iid"
        Write-Host "    Name: $actualName (expected: $expected)"
        Write-Host "    ProxyStub: $ps"
        Write-Host "    NumMethods: $nm"
        Write-Host ""
        $intKey.Close()
    } else {
        Write-Host "MISS  $iid ($expected) - NOT IN REGISTRY"
        Write-Host ""
    }
}

# Also list ALL subkeys under each interface
Write-Host "=== Full subkey listing for IFdtCommunication ==="
$ifc = $key32.OpenSubKey("Interface\{039ECFC4-9CA8-44E6-944D-B37F288A34D8}")
if ($ifc) {
    Write-Host "  Name: $($ifc.GetValue(''))"
    foreach ($sub in $ifc.GetSubKeyNames()) {
        $sk = $ifc.OpenSubKey($sub)
        Write-Host "  $sub : $($sk.GetValue(''))"
        $sk.Close()
    }
    $ifc.Close()
}

$key32.Close()
