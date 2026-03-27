$cfg = @"
<?xml version="1.0"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" />
  </startup>
  <runtime>
    <generatePublisherEvidence enabled="false" />
  </runtime>
</configuration>
"@
# Use ASCII/no BOM encoding - PACTware may not handle UTF8 BOM
[System.IO.File]::WriteAllText("C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\PACTWARE.exe.config", $cfg, (New-Object System.Text.UTF8Encoding $false))
Write-Host "Config restaurado sem BOM!"
