$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$newInstall = Join-Path $env:LOCALAPPDATA 'UniversalClipboardPaste'
$oldInstall = Join-Path $env:LOCALAPPDATA 'ChatGPTClipboardFilePaste'
$src = Join-Path $root 'src\UniversalClipboardPaste.cs'
$candidate = Join-Path $newInstall 'UniversalClipboardPaste_candidate.exe'
$target = Join-Path $newInstall 'UniversalClipboardPaste.exe'
$oldTarget = Join-Path $oldInstall 'ChatGPTClipboardFilePaste.exe'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -ItemType Directory -Force -Path $newInstall | Out-Null
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /out:$candidate /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" $src
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Stop-Process -Name UniversalClipboardPaste -Force -ErrorAction SilentlyContinue
Stop-Process -Name ChatGPTClipboardFilePaste -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250
Copy-Item $candidate $target -Force
Remove-Item $candidate -Force
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'UniversalClipboardPaste' -Value ('"' + $target + '"')
Start-Process $target
Start-Sleep -Milliseconds 700
$newProcess = Get-Process UniversalClipboardPaste -ErrorAction SilentlyContinue
if (-not $newProcess) {
    if (Test-Path $oldTarget) {
        Set-ItemProperty -Path $runKey -Name 'UniversalClipboardPaste' -Value ('"' + $oldTarget + '"')
        Start-Process $oldTarget
    }
    throw 'New UniversalClipboardPaste process did not start; old version restored when available.'
}
if (Test-Path $oldInstall) {
    $oldLog = Join-Path $oldInstall 'hotkey.log'
    if ((Test-Path $oldLog) -and -not (Test-Path (Join-Path $newInstall 'hotkey_pre_1.2.log'))) {
        Copy-Item $oldLog (Join-Path $newInstall 'hotkey_pre_1.2.log') -Force
    }
    Remove-Item $oldInstall -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'DEPLOY_OK'