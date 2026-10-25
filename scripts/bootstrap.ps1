param([int]$WaitForPid = 0)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = Split-Path $PSScriptRoot
$data = Join-Path $env:LOCALAPPDATA 'GenevoObserver'
New-Item -ItemType Directory -Force $data | Out-Null
$lock = $null
try {
    $lock = [IO.File]::Open((Join-Path $data 'update.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    if ($WaitForPid -gt 0) { Wait-Process -Id $WaitForPid -ErrorAction SilentlyContinue }
    $exe = Join-Path $root 'GenevoLauncher.exe'
    if (!(Test-Path $exe)) { $exe = Join-Path $root 'dist\GenevoLauncher.exe' }
    $versionFile = Join-Path (Split-Path $exe) 'VERSION'
    $localVersion = [version](Get-Content $versionFile -Raw).Trim()
    $pointer = Join-Path $data 'current.txt'
    if (Test-Path $pointer) {
        $saved = (Get-Content $pointer -Raw).Trim()
        $managed = Join-Path $data "versions\$saved"
        if ($saved -match '^\d+\.\d+\.\d+$' -and (Test-Path "$managed\GenevoLauncher.exe") -and [version]$saved -ge $localVersion) {
            $exe = "$managed\GenevoLauncher.exe"; $localVersion = [version]$saved
        }
    }
    $status = '업데이트 확인 완료'
    try {
        $release = Invoke-RestMethod 'https://api.github.com/repos/japanoxx-afk/genevo/releases/latest' -Headers @{'User-Agent'='GenevoObserver'} -TimeoutSec 15
        $remote = [version]($release.tag_name -replace '^v','')
        if ($remote -gt $localVersion) {
            $zipAsset = @($release.assets | Where-Object name -eq 'genevo-win-x64.zip')
            $hashAsset = @($release.assets | Where-Object name -eq 'genevo-win-x64.zip.sha256')
            if ($zipAsset.Count -ne 1 -or $hashAsset.Count -ne 1) { throw 'Release assets missing' }
            foreach ($asset in @($zipAsset[0],$hashAsset[0])) {
                if ($asset.browser_download_url -notlike 'https://github.com/japanoxx-afk/genevo/releases/download/*') { throw 'Unexpected download URL' }
            }
            $stage = Join-Path $data ('stage-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory $stage | Out-Null
            $zip = Join-Path $stage 'package.zip'
            Invoke-WebRequest $zipAsset[0].browser_download_url -OutFile $zip -UseBasicParsing -TimeoutSec 120
            $hashFile = Join-Path $stage 'package.sha256'
            Invoke-WebRequest $hashAsset[0].browser_download_url -OutFile $hashFile -UseBasicParsing -TimeoutSec 15
            $expected = (Get-Content $hashFile -Raw).Trim().Split(' ')[0]
            if ($expected -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) { throw 'SHA256 mismatch' }
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $archive = [IO.Compression.ZipFile]::OpenRead($zip)
            try { foreach ($entry in $archive.Entries) {
                if ($entry.FullName -notin @('GenevoLauncher.exe','VERSION','scripts/bootstrap.ps1','scripts/','scripts\bootstrap.ps1')) { throw 'Unexpected archive entry' }
            }} finally { $archive.Dispose() }
            $expanded = Join-Path $stage 'app'
            Expand-Archive $zip $expanded
            if (!(Test-Path "$expanded\GenevoLauncher.exe") -or !(Test-Path "$expanded\scripts\bootstrap.ps1") -or [version](Get-Content "$expanded\VERSION" -Raw).Trim() -ne $remote) { throw 'Invalid package' }
            $dest = Join-Path $data "versions\$remote"
            New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
            if (Test-Path $dest) { throw 'Version directory already exists; inspect previous installation' }
            Move-Item -LiteralPath $expanded -Destination $dest
            [IO.File]::WriteAllText("$pointer.new", "$remote")
            Move-Item -LiteralPath "$pointer.new" -Destination $pointer -Force
            $exe = "$dest\GenevoLauncher.exe"
            $status = "업데이트 완료: $remote"
        }
    } catch {
        $status = '업데이트 확인/설치 실패 · 기존 버전 실행'
        Add-Content (Join-Path $data 'update.log') "$(Get-Date -Format o) $($_.Exception.Message)"
    }
    Set-Content (Join-Path $data 'update-status.txt') $status -Encoding UTF8
    Start-Process -FilePath $exe -ArgumentList '--checked' -WorkingDirectory (Split-Path $exe)
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Genevo 실행 오류') | Out-Null
} finally { if ($lock) { $lock.Dispose() } }
