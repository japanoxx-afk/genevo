$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /out:"$out\GenevoLauncher.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$root\src\Launcher.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Copy-Item "$root\VERSION" $out
New-Item -ItemType Directory -Force "$out\scripts" | Out-Null
Copy-Item "$PSScriptRoot\bootstrap.ps1" "$out\scripts"
Write-Host "Built $out\GenevoLauncher.exe"
