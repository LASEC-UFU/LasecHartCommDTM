# Adiciona HART Bus Category e demais categorias FDT faltando no registro (WOW6432Node / 32-bit)
$hklm32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry32')

$clsid = 'SOFTWARE\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}'
$cats = @(
    '{036D1498-387B-11D4-86E1-00E0987270B9}',
    '{036D1493-387B-11D4-86E1-00E0987270B9}',
    '{036D1494-387B-11D4-86E1-00E0987270B9}',
    '{036D1495-387B-11D4-86E1-00E0987270B9}'
)

foreach ($cat in $cats) {
    $kpath = "$clsid\Implemented Categories\$cat"
    $k = $hklm32.CreateSubKey($kpath)
    $k.SetValue('', '')
    $k.Close()
    Write-Host "Added: $cat"
}

$hk = $hklm32.CreateSubKey('SOFTWARE\Classes\Component Categories\{036D1498-387B-11D4-86E1-00E0987270B9}')
$hk.SetValue('', 'HART')
$hk.Close()
$hklm32.Close()

Write-Host 'OK. Verificando Implemented Categories...'
reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}\Implemented Categories" /s
