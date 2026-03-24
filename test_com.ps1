try {
    $t   = [System.Type]::GetTypeFromCLSID([Guid]'{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}')
    $obj = [System.Activator]::CreateInstance($t)
    Write-Output ("OK: " + $obj.GetType().FullName)
} catch {
    Write-Output ("ERRO: " + $_.Exception.ToString())
}
