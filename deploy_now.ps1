# Quick deploy - run this in an ADMIN PowerShell
$ErrorActionPreference = "Stop"
$src    = "C:\SourceCode\LasecHartCommDTM\src\LasecHartCommDTM\bin\x86\Release\net48\LasecHartCommDTM.dll"
$dst    = "C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\LasecHartCommDTM.dll"
$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

Copy-Item $src $dst -Force
Write-Host "Copied: $(Get-Item $dst | % { "$($_.Length) bytes $($_.LastWriteTime)" })"
& $regasm /u $dst 2>&1 | Out-Null
& $regasm /codebase /tlb $dst
$logFile = "C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log"
[System.IO.File]::WriteAllText($logFile, "", [System.Text.Encoding]::UTF8)
Write-Host "Done. Log cleared."
