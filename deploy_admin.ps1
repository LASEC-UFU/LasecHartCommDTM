# Run as Administrator
$src    = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\x86\Release\net48"
$dtmDir = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM"
$dtmDll = "$dtmDir\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

Write-Host "1. Copying DLL from $src..."
New-Item -ItemType Directory -Force -Path "$dtmDir\Dtm" | Out-Null
Copy-Item "$src\LasecHartCommDTM.dll" "$dtmDir\Dtm\" -Force
Write-Host "   Done: $(Get-Item $dtmDll | Select-Object -ExpandProperty LastWriteTime)"

Write-Host "2. Unregistering old COM registration..."
# IMPORTANT: Do NOT use /u /tlb — it deletes FDT interface entries from registry!
& $regasm /u $dtmDll 2>&1 | Out-Null
$regasm2 = "C:\Windows\Microsoft.NET\Framework\v2.0.50727\regasm.exe"
if (Test-Path $regasm2) { & $regasm2 /u $dtmDll 2>&1 | Out-Null }

Write-Host "3. Registering with CLR 4.0 regasm (x86) + TypeLib..."
# /codebase = registers CodeBase pointing to assembly on disk
# /tlb = generates TypeLib — REQUIRED for PACTware to discover event interfaces
# (IConnectionPointContainer). Without it, PACTware never subscribes to events
# and the DTM never goes green.
# SIDE EFFECT: /tlb hijacks FDT interface entries → Step 4 fixes this.
& $regasm /codebase /tlb $dtmDll

Write-Host "4. Safety check: ensuring FDT interface registrations are intact..."
# Without /tlb these should not be touched, but verify just in case.
# Also re-registers FDT100.dll TypeLib to repair any prior damage.
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class TlbReg {
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    public static extern int LoadTypeLibEx(string szFile, int regkind, out IntPtr pptlib);
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterTypeLib(IntPtr ptlib, string szFullPath, string szHelpDir);
    public static int Register(string path) {
        IntPtr ptlib;
        int hr = LoadTypeLibEx(path, 2, out ptlib);
        if (hr != 0) return hr;
        hr = RegisterTypeLib(ptlib, path, null);
        Marshal.Release(ptlib);
        return hr;
    }
}
"@
$fdt100 = "C:\WINDOWS\SysWOW64\FDT100.dll"
if (Test-Path $fdt100) {
    $hr = [TlbReg]::Register($fdt100)
    if ($hr -eq 0) { Write-Host "   FDT100.dll TypeLib re-registered OK" }
    else { Write-Host "   WARNING: FDT100.dll register failed (0x$($hr.ToString('X8')))" }
}
# Verify interfaces
$jigfdtTlb = "{036D1471-387B-11D4-86E1-00E0987270B9}"
$jigfdtVer = "1.4e84"
# Hashtable: IID -> correct interface name
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
$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)
$fixCount = 0
foreach ($iid in $fdtInterfaces.Keys) {
    $correctName = $fdtInterfaces[$iid]
    # Fix TypeLib reference
    $tlKey = $key32.OpenSubKey("Interface\$iid\TypeLib", $true)
    if ($tlKey) {
        $current = $tlKey.GetValue("")
        if ($current -ne $jigfdtTlb) {
            $tlKey.SetValue("", $jigfdtTlb)
            $tlKey.SetValue("Version", $jigfdtVer)
            $fixCount++
            Write-Host "   Fixed TypeLib $iid ($correctName)"
        }
        $tlKey.Close()
    }
    # Fix interface name (regasm may prefix with namespace)
    $intKey = $key32.OpenSubKey("Interface\$iid", $true)
    if ($intKey) {
        $currentName = $intKey.GetValue("")
        if ($currentName -ne $correctName) {
            $intKey.SetValue("", $correctName)
            $fixCount++
            Write-Host "   Fixed Name $iid : $currentName -> $correctName"
        }
        $intKey.Close()
    }
}
$key32.Close()
Write-Host "   Restored $fixCount interface(s) to Jigfdt TypeLib"

Write-Host "5. Registering ConfigControl ActiveX keys..."
$ctrlClsid = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}"
if (Test-Path $ctrlClsid) {
    New-Item -Path "$ctrlClsid\Control" -Force | Out-Null
    New-Item -Path "$ctrlClsid\MiscStatus" -Force | Out-Null
    Set-ItemProperty -Path "$ctrlClsid\MiscStatus" -Name "(Default)" -Value "0"
    New-Item -Path "$ctrlClsid\MiscStatus\1" -Force | Out-Null
    Set-ItemProperty -Path "$ctrlClsid\MiscStatus\1" -Name "(Default)" -Value "131473"
    New-Item -Path "$ctrlClsid\VERSION" -Force | Out-Null
    Set-ItemProperty -Path "$ctrlClsid\VERSION" -Name "(Default)" -Value "1.0"
    Write-Host "   ConfigControl ActiveX keys created."
} else {
    Write-Host "   WARNING: ConfigControl CLSID not found!"
}

Write-Host "6. Clearing log..."
$logFile = "C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log"
[System.IO.File]::WriteAllText($logFile, "", [System.Text.Encoding]::UTF8)

Write-Host "7. Verificando registro completo..."
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}" /s 2>&1

Write-Host ""
Write-Host "Done. Run PACTware > Update Catalog."
Write-Host "Then check: type `"$logFile`""
