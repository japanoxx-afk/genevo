$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
& "$PSScriptRoot\build.ps1"
$version = (Get-Content "$root\VERSION" -Raw).Trim()
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw 'Tag and VERSION differ' }
$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $out | Out-Null
$zip = Join-Path $out 'genevo-win-x64.zip'
Compress-Archive -Path "$root\dist\*" -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  genevo-win-x64.zip`n", [Text.Encoding]::ASCII)
Write-Host "Packaged v$version"
