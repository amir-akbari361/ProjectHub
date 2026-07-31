# Generates a 2048-bit RSA private key in PKCS#8 PEM format for local JWT signing.
# The file is git-ignored; in Docker/prod the key is supplied via the Jwt__PrivateKeyPem env var.
#
# We shell out to `dotnet` so this runs on the SAME modern .NET runtime the app uses, whose RSA type
# has ExportPkcs8PrivateKeyPem(). This sidesteps Windows PowerShell 5.1 (.NET Framework), whose RSACng
# lacks that API entirely — the reason a pure-PowerShell approach fails on default Windows shells.
$ErrorActionPreference = 'Stop'

$keyDir  = Join-Path $PSScriptRoot '..\src\ProjectHub.API\keys'
$keyPath = Join-Path $keyDir 'jwt-private.pem'

New-Item -ItemType Directory -Force -Path $keyDir | Out-Null

$fullKeyPath = [System.IO.Path]::GetFullPath($keyPath)

# A tiny C# program executed via `dotnet run` inside a throwaway temp project. Writing the PEM in C#
# guarantees a valid PKCS#8 encoding identical to what JwtProvider.ImportFromPem() expects at runtime.
$csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@

$program = @"
using System.Security.Cryptography;
var rsa = RSA.Create(2048);
var pem = rsa.ExportPkcs8PrivateKeyPem();
File.WriteAllText(@"$fullKeyPath", pem);
Console.WriteLine("Wrote " + @"$fullKeyPath");
"@

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("jwtkeygen-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
try {
    Set-Content -Path (Join-Path $tempDir 'keygen.csproj') -Value $csproj
    Set-Content -Path (Join-Path $tempDir 'Program.cs')     -Value $program
    dotnet run --project $tempDir -c Release | Write-Host
}
finally {
    Remove-Item -Recurse -Force $tempDir
}
