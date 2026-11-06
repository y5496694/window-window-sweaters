param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$version = '0.1.0'
$stage = Join-Path $PSScriptRoot ('bin\package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\WindowWindowSweaters.exe') -Destination $stage
foreach ($name in @('README.md', 'NOTICE.md', 'LICENSE', 'CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage }
New-Item -ItemType Directory -Path (Join-Path $stage 'source') -Force | Out-Null
foreach ($name in @('src', 'tests', 'tools', 'app.manifest', 'build.ps1', 'package.ps1', 'README.md', 'NOTICE.md', 'LICENSE', 'CHANGELOG.md', 'docs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'source') -Recurse
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $stage -Recurse
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zip = Join-Path $OutputDirectory "WindowWindowSweaters-$version-windows.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Packaged $zip"
