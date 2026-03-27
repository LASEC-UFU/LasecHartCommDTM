# Restore ALL FDT interface entries in HKCR\Interface (x86 view)
# These were deleted by regasm /u /tlb and must be fully recreated

$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$jigfdtTlb = "{036D1471-387B-11D4-86E1-00E0987270B9}"
$jigfdtVer = "1.4e84"
$oleAutProxy = "{00020424-0000-0000-C000-000000000046}"
$oleAutDispatch = "{00020420-0000-0000-C000-000000000046}"

# All FDT interfaces with their correct data
# Format: IID, Name, ProxyStubClsid32
$fdtInterfaces = @(
    @("{036D147F-387B-11D4-86E1-00E0987270B9}", "IDtmInformation",         $oleAutProxy),
    @("{036D1481-387B-11D4-86E1-00E0987270B9}", "IDtm",                    $oleAutProxy),
    @("{039ECFC4-9CA8-44E6-944D-B37F288A34D8}", "IFdtCommunication",       $oleAutProxy),
    @("{036D147D-387B-11D4-86E1-00E0987270B9}", "IDtmParameter",           $oleAutProxy),
    @("{036D1480-387B-11D4-86E1-00E0987270B9}", "IDtmActiveXInformation",  $oleAutProxy),
    @("{036D1486-387B-11D4-86E1-00E0987270B9}", "IDtmActiveXControl",      $oleAutProxy),
    @("{036D1478-387B-11D4-86E1-00E0987270B9}", "IFdtEvents",              $oleAutProxy),
    @("{036D1484-387B-11D4-86E1-00E0987270B9}", "IFdtChannelSubTopology",  $oleAutProxy),
    @("{036D1489-387B-11D4-86E1-00E0987270B9}", "IDtmChannel",             $oleAutProxy),
    @("{036D1488-387B-11D4-86E1-00E0987270B9}", "IFdtChannel",             $oleAutProxy),
    @("{F15BA42E-BBF1-42ED-8009-7F664A002CFB}", "IDtmEventsSource",        $oleAutDispatch),
    @("{036D147C-387B-11D4-86E1-00E0987270B9}", "IDtmDocumentation",       $oleAutProxy),
    @("{036D1485-387B-11D4-86E1-00E0987270B9}", "IFdtCommunicationEvents", $oleAutProxy),
    @("{E4F31A10-45BF-11D4-BBB3-0060080993FF}", "IFdtChannelCollection",   $oleAutProxy)
)

$created = 0
$fixed = 0

foreach ($entry in $fdtInterfaces) {
    $iid = $entry[0]
    $name = $entry[1]
    $proxyClsid = $entry[2]
    $path = "Interface\$iid"
    
    # Create or open interface key
    $intKey = $key32.OpenSubKey($path, $true)
    if (-not $intKey) {
        $intKey = $key32.CreateSubKey($path)
        $created++
        Write-Host "CREATED  $name ($iid)"
    }
    
    # Set interface name
    $currentName = $intKey.GetValue("")
    if ($currentName -ne $name) {
        $intKey.SetValue("", $name)
        if ($currentName) { Write-Host "  Renamed: $currentName -> $name" }
    }
    
    # ProxyStubClsid
    $psKey = $intKey.OpenSubKey("ProxyStubClsid", $true)
    if (-not $psKey) { $psKey = $intKey.CreateSubKey("ProxyStubClsid") }
    $psKey.SetValue("", $proxyClsid)
    $psKey.Close()
    
    # ProxyStubClsid32
    $ps32Key = $intKey.OpenSubKey("ProxyStubClsid32", $true)
    if (-not $ps32Key) { $ps32Key = $intKey.CreateSubKey("ProxyStubClsid32") }
    $ps32Key.SetValue("", $proxyClsid)
    $ps32Key.Close()
    
    # TypeLib
    $tlKey = $intKey.OpenSubKey("TypeLib", $true)
    if (-not $tlKey) { $tlKey = $intKey.CreateSubKey("TypeLib") }
    $current = $tlKey.GetValue("")
    if ($current -ne $jigfdtTlb) {
        $tlKey.SetValue("", $jigfdtTlb)
        $tlKey.SetValue("Version", $jigfdtVer)
        $fixed++
    }
    $tlKey.Close()
    $intKey.Close()
}

$key32.Close()
Write-Host ""
Write-Host "Created $created interface(s), fixed $fixed TypeLib ref(s)"
Write-Host ""
Write-Host "Now close PACTware completely, reopen, and test CWHart."
