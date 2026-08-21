[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13 -bor [Net.SecurityProtocolType]::Tls11 -bor [Net.SecurityProtocolType]::Ssl3
[Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

try {
    Write-Host "Intentando conexion a https://sistemas.cbmw.cl/lab-config.json..."
    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add("User-Agent", "CBMW-Agent/2.0")
    $wc.Headers.Add("Cache-Control", "no-cache")
    $res = $wc.DownloadString("https://sistemas.cbmw.cl/lab-config.json")
    Write-Host "EXITO EN CONEXION:" -ForegroundColor Green
    Write-Host $res
} catch {
    Write-Host "ERROR CONEXION:" -ForegroundColor Red
    Write-Host $_.Exception.ToString()
}
