$k = [Microsoft.Win32.RegistryKey]::OpenBaseKey('ClassesRoot', 'Registry32').OpenSubKey("CLSID\{6358CCBF-AA0E-4633-9564-9EEFB7FDE86D}\InprocServer32", $true)
$k.SetValue("", "C:\Windows\SysWow64\CWHARTFDT.ocx")
$k.SetValue("ThreadingModel", "Apartment")
foreach ($n in @("Assembly","Class","RuntimeVersion","CodeBase")) { try { $k.DeleteValue($n) } catch {} }
$k.Close()
Write-Host "RESTORED to CWHARTFDT.ocx" -ForegroundColor Green
