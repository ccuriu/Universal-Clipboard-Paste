$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$install = Join-Path $env:LOCALAPPDATA 'UniversalClipboardPaste'
$src = Join-Path $root 'src\UniversalClipboardPaste.cs'
$candidate = Join-Path $install 'UniversalClipboardPaste_candidate.exe'
$target = Join-Path $install 'UniversalClipboardPaste.exe'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

New-Item -ItemType Directory -Force -Path $install | Out-Null
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /out:$candidate /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" $src
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }

Stop-Process -Name UniversalClipboardPaste -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250
Copy-Item $candidate $target -Force
Remove-Item $candidate -Force
Remove-Item (Join-Path $install 'hotkey.log') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $install 'payloads') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:TEMP 'UniversalClipboardPaste') -Recurse -Force -ErrorAction SilentlyContinue

New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'UniversalClipboardPaste' -Value ('"' + $target + '"')
Start-Process $target
Start-Sleep -Milliseconds 500

if (-not (Get-Process UniversalClipboardPaste -ErrorAction SilentlyContinue)) {
    throw 'UniversalClipboardPaste did not start.'
}
Write-Host 'DEPLOY_OK'