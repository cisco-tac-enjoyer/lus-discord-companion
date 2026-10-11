$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

New-Item -ItemType Directory -Force dist | Out-Null

# One .exe compiled with the C# compiler that ships with Windows (.NET Framework 4.8).
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
$src = 'lus-discord-companion'
& $csc /nologo /target:winexe /optimize+ /utf8output `
  "/win32icon:$src/Assets/app.ico" `
  /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  /out:dist/lus-discord-companion.exe "$src/*.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
  dotnet test lus-discord-companion.Tests
  if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} else {
  Write-Host 'dotnet SDK not installed, skipping tests.'
}

Write-Host 'Built dist/lus-discord-companion.exe'
