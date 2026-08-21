$ErrorActionPreference = 'SilentlyContinue'

function Test-Port($port) {
    $c = New-Object System.Net.Sockets.TcpClient
    try {
        $c.Connect('127.0.0.1', $port)
        return $c.Connected
    } catch {
        return $false
    } finally {
        $c.Close()
    }
}

$web = Test-Port 5099
$api = Test-Port 5216
Write-Output ("WEB_5099=" + $web)
Write-Output ("API_5216=" + $api)
