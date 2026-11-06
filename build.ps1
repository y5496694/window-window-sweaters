param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler is required.' }
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /warnaserror+ /main:WindowSweaters.Program "/win32manifest:$root\app.manifest" "/out:$bin\WindowWindowSweaters.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    & $compiler /nologo /target:exe /platform:anycpu /optimize+ /utf8output /warnaserror+ /main:WindowSweaters.Tests "/win32manifest:$root\app.manifest" "/out:$bin\SweatersTests.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources "$root\tests\Tests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & "$bin\SweatersTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Host "Built $bin\WindowWindowSweaters.exe"
