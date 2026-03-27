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
Set-Content -Path "C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\PACTWARE.exe.config" -Value $cfg -Encoding UTF8
Write-Host "Config restaurado!"
Get-Content "C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\PACTWARE.exe.config"
