$cfg = 'C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\PACTWARE.exe.config'
$logPath = 'C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\pactware.log'

$xml = @"
<?xml version="1.0"?>
<configuration>
  <runtime>
    <generatePublisherEvidence enabled="false" />
  </runtime>
  <log4net>
    <appender name="FileAppender" type="log4net.Appender.FileAppender">
      <file value="$logPath" />
      <appendToFile value="false" />
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%date [%thread] %-5level %logger - %message%newline%exception" />
      </layout>
    </appender>
    <root>
      <level value="DEBUG" />
      <appender-ref ref="FileAppender" />
    </root>
  </log4net>
</configuration>
"@

Set-Content -Path $cfg -Value $xml -Encoding UTF8
Write-Host "Log habilitado em: $logPath"
Write-Host "Agora: 1) Abra o PACTware  2) Faça Update Catalog  3) Feche o PACTware"
