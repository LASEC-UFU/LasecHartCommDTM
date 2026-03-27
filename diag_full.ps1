# Complete diagnostic - compare ALL registry state
# Run BEFORE and AFTER unregistering our DTM

$ourTlb = "{40498B38-0A79-3F68-905F-7954AA76AEA6}"

$key32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::ClassesRoot,
    [Microsoft.Win32.RegistryView]::Registry32)

# 1. Find ALL interfaces pointing to our TypeLib
Write-Host "=== ALL Interfaces pointing to OUR TypeLib ==="
$intKey = $key32.OpenSubKey("Interface")
foreach ($iid in $intKey.GetSubKeyNames()) {
    $tlKey = $intKey.OpenSubKey("$iid\TypeLib")
    if ($tlKey) {
        $g = $tlKey.GetValue("")
        if ($g -eq $ourTlb) {
            $nameKey = $intKey.OpenSubKey($iid)
            $name = $nameKey.GetValue("")
            Write-Host "  {$iid}  $name"
            $nameKey.Close()
        }
        $tlKey.Close()
    }
}
$intKey.Close()

# 2. Find ALL interfaces whose names start with LasecHartCommDTM_
Write-Host ""
Write-Host "=== ALL Interfaces with names containing 'Lasec' or 'Hart' ==="
$intKey = $key32.OpenSubKey("Interface")
foreach ($iid in $intKey.GetSubKeyNames()) {
    $nameKey = $intKey.OpenSubKey($iid)
    $name = $nameKey.GetValue("")
    if ($name -and ($name -match "Lasec|Hart|CommDtm")) {
        $tlKey = $intKey.OpenSubKey("$iid\TypeLib")
        $tl = if ($tlKey) { $tlKey.GetValue(""); $tlKey.Close() } else { "NONE" }
        Write-Host "  {$iid}  $name  TL=$tl"
    }
    $nameKey.Close()
}
$intKey.Close()

# 3. Check our TypeLib - list ALL interfaces that reference it in TypeLib\{GUID}\version\0
Write-Host ""
Write-Host "=== Our TypeLib TLB file ==="
$tlbPath = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\LasecHartCommDTM.tlb"
if (Test-Path $tlbPath) {
    $f = Get-Item $tlbPath
    Write-Host "  $tlbPath  Size=$($f.Length)  Modified=$($f.LastWriteTime)"
} else {
    Write-Host "  TLB file NOT FOUND"
}

# 4. Check if there are CLSID entries for CWHart sub-interfaces
Write-Host ""
Write-Host "=== CWHart related CLSIDs ==="
$clsidKey = $key32.OpenSubKey("CLSID")
$cwClsids = @(
    "{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}",  # CWHart main
    "{A7B10993-E tried all 8 categories"
)
# Just check the main one
$cw = $clsidKey.OpenSubKey("{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}")
if ($cw) {
    Write-Host "  CWHart CLSID exists"
    foreach ($sub in $cw.GetSubKeyNames()) {
        $sk = $cw.OpenSubKey($sub)
        $val = $sk.GetValue("")
        Write-Host "    $sub = $val"
        # List sub-subkeys
        foreach ($ss in $sk.GetSubKeyNames()) {
            $ssk = $sk.OpenSubKey($ss)
            Write-Host "      $ss = $($ssk.GetValue(''))"
            $ssk.Close()
        }
        $sk.Close()
    }
    $cw.Close()
}
$clsidKey.Close()

# 5. Dump the entire snapshot of before_reinstall interfaces if available
Write-Host ""
Write-Host "=== Comparing with fresh PACTware snapshot ==="
$snapshotFile = "C:\SourceCode\LasecHartCommDTM\pactware_snapshot_after\interfaces_fresh.txt"
if (Test-Path $snapshotFile) {
    Write-Host "Fresh snapshot exists at $snapshotFile"
    Get-Content $snapshotFile | Select-Object -First 30
} else {
    Write-Host "No fresh snapshot found"
}

$key32.Close()
