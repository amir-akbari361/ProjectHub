$ErrorActionPreference = 'SilentlyContinue'

# Free port 5099 if a previous run is still holding it.
$conns = Get-NetTCPConnection -LocalPort 5099 -State Listen
foreach ($c in $conns) {
    Write-Output ("Killing stale PID {0} on 5099" -f $c.OwningProcess)
    Stop-Process -Id $c.OwningProcess -Force
}
Start-Sleep -Seconds 1

$outFile = Join-Path $PSScriptRoot 'run.out.txt'
$errFile = Join-Path $PSScriptRoot 'run.err.txt'

$p = Start-Process dotnet `
    -ArgumentList 'run --project src/ProjectHub.Web/ProjectHub.Web.csproj --no-build' `
    -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile

Start-Sleep -Seconds 12

if ($p.HasExited) {
    Write-Output ("EXITED code=" + $p.ExitCode)
}
else {
    Write-Output "RUNNING-OK"
    Stop-Process -Id $p.Id -Force
}

Write-Output "----- STDOUT -----"
Get-Content $outFile
Write-Output "----- STDERR -----"
Get-Content $errFile
