# Re-register FDT TypeLib and CWHart to restore all original entries
# This fixes the damage caused by regasm /u /tlb

Write-Host "1. Re-registering Jigfdt TypeLib (FDT100.dll)..."
$fdt100 = "C:\WINDOWS\SysWOW64\FDT100.dll"
if (Test-Path $fdt100) {
    # Use LoadTypeLibEx + RegisterTypeLib via .NET interop
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class TypeLibHelper {
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    public static extern int LoadTypeLibEx(string szFile, int regkind, out IntPtr pptlib);

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterTypeLib(IntPtr ptlib, string szFullPath, string szHelpDir);

    public static int Register(string path) {
        IntPtr ptlib;
        // REGKIND_NONE = 2
        int hr = LoadTypeLibEx(path, 2, out ptlib);
        if (hr != 0) return hr;
        hr = RegisterTypeLib(ptlib, path, null);
        Marshal.Release(ptlib);
        return hr;
    }
}
"@
    $hr = [TypeLibHelper]::Register($fdt100)
    if ($hr -eq 0) {
        Write-Host "   FDT100.dll TypeLib registered successfully"
    } else {
        Write-Host "   FAILED to register FDT100.dll TypeLib. HRESULT: 0x$($hr.ToString('X8'))"
    }
} else {
    Write-Host "   FDT100.dll NOT FOUND at $fdt100"
}

Write-Host ""
Write-Host "2. Re-registering CWHart (CWHARTFDT.ocx)..."
$cwOcx = "C:\Windows\SysWow64\CWHARTFDT.ocx"
if (Test-Path $cwOcx) {
    $result = & regsvr32 /s $cwOcx 2>&1
    Write-Host "   regsvr32 CWHARTFDT.ocx done (exit=$LASTEXITCODE)"
} else {
    Write-Host "   CWHARTFDT.ocx NOT FOUND"
}

Write-Host ""
Write-Host "3. Verifying FDT interfaces after re-registration..."
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

$jigfdtTlb = "{036D1471-387B-11D4-86E1-00E0987270B9}"
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

foreach ($iid in $interfaces.Keys) {
    $name = $interfaces[$iid]
    $intKey = $key32.OpenSubKey("Interface\$iid")
    if ($intKey) {
        $actualName = $intKey.GetValue("")
        $tlKey = $intKey.OpenSubKey("TypeLib")
        $tl = if ($tlKey) { $tlKey.GetValue(""); $tlKey.Close() } else { "NONE" }
        $status = if ($tl -eq $jigfdtTlb) { "OK" } else { "WRONG(TL=$tl)" }
        $nameStatus = if ($actualName -eq $name) { "" } else { " RENAMED=$actualName" }
        Write-Host "  $status  $name$nameStatus"
        $intKey.Close()
    } else {
        Write-Host "  MISS  $name"
    }
}

# Also verify CWHart CLSID
Write-Host ""
Write-Host "4. CWHart CLSID status..."
$cwKey = $key32.OpenSubKey("CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}")
if ($cwKey) {
    Write-Host "  CWHart CLSID: EXISTS"
    $catKey = $cwKey.OpenSubKey("Implemented Categories")
    if ($catKey) {
        Write-Host "  Categories: $($catKey.GetSubKeyNames().Count)"
        $catKey.Close()
    }
    $cwKey.Close()
} else {
    Write-Host "  CWHart CLSID: MISSING!"
}

$key32.Close()

Write-Host ""
Write-Host "Done. Close PACTware completely and reopen to test CWHart."
