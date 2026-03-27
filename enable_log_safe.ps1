# Habilita log do PACTware SEM destruir o config original
$cfgPath = "C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\PACTWARE.exe.config"
$logPath = "C:\Temp\pactware.log"

$xml = [xml](Get-Content $cfgPath)

# Adiciona configSections se nao existe
$cs = $xml.configuration.SelectSingleNode("configSections")
if (-not $cs) {
    $cs = $xml.CreateElement("configSections")
    # configSections DEVE ser o primeiro filho de <configuration>
    $xml.configuration.PrependChild($cs) | Out-Null
}

# Adiciona section log4net se nao existe
$sect = $cs.SelectSingleNode("section[@name='log4net']")
if (-not $sect) {
    $sect = $xml.CreateElement("section")
    $sect.SetAttribute("name", "log4net")
    $sect.SetAttribute("type", "log4net.Config.Log4NetConfigurationSectionHandler, log4net")
    $cs.AppendChild($sect) | Out-Null
}

# Adiciona bloco log4net se nao existe
$l4n = $xml.configuration.SelectSingleNode("log4net")
if (-not $l4n) {
    $l4n = $xml.CreateElement("log4net")
    $xml.configuration.AppendChild($l4n) | Out-Null

    $appender = $xml.CreateElement("appender")
    $appender.SetAttribute("name", "FileAppender")
    $appender.SetAttribute("type", "log4net.Appender.FileAppender")

    $file = $xml.CreateElement("file")
    $file.SetAttribute("value", $logPath)
    $appender.AppendChild($file) | Out-Null

    $append = $xml.CreateElement("appendToFile")
    $append.SetAttribute("value", "false")
    $appender.AppendChild($append) | Out-Null

    $layout = $xml.CreateElement("layout")
    $layout.SetAttribute("type", "log4net.Layout.PatternLayout")
    $cp = $xml.CreateElement("conversionPattern")
    $cp.SetAttribute("value", "%date [%thread] %-5level %logger - %message%newline%exception")
    $layout.AppendChild($cp) | Out-Null
    $appender.AppendChild($layout) | Out-Null

    $l4n.AppendChild($appender) | Out-Null

    $root = $xml.CreateElement("root")
    $level = $xml.CreateElement("level")
    $level.SetAttribute("value", "DEBUG")
    $root.AppendChild($level) | Out-Null
    $aref = $xml.CreateElement("appender-ref")
    $aref.SetAttribute("ref", "FileAppender")
    $root.AppendChild($aref) | Out-Null
    $l4n.AppendChild($root) | Out-Null
}

$xml.Save($cfgPath)
Write-Host "Log habilitado em: $logPath"
Write-Host "Config preservado:"
Get-Content $cfgPath
