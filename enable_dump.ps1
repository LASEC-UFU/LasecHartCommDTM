# Enable WER local dump for PACTware.exe
# Run as Administrator

$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\PACTware.exe"
$dumpDir = "C:\Temp\PACTwareDumps"

New-Item -Path $dumpDir -ItemType Directory -Force | Out-Null
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "DumpFolder" -Value $dumpDir -Type ExpandString
Set-ItemProperty -Path $regPath -Name "DumpCount" -Value 5 -Type DWord
Set-ItemProperty -Path $regPath -Name "DumpType" -Value 2 -Type DWord  # Full dump

Write-Host "WER dump enabled. Dumps will be saved to: $dumpDir"
Write-Host "Now run PACTware > Update Catalog, then check the dump folder."
