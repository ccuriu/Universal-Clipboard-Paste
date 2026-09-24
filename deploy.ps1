$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$install = Join-Path $env:LOCALAPPDATA 'ChatGPTClipboardFilePaste'
$src = Join-Path $root 'src\ChatGPTClipboardFilePaste.cs'
$candidate = Join-Path $install 'ChatGPTClipboardFilePaste_candidate.exe'
$target = Join-Path $install 'ChatGPTClipboardFilePaste.exe'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path $install | Out-Null
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /out:$candidate /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" $src
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Stop-Process -Name ChatGPTClipboardFilePaste -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250
Copy-Item $candidate $target -Force
Copy-Item $src (Join-Path $install 'ChatGPTClipboardFilePaste.cs') -Force
Remove-Item $candidate -Force

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'UniversalClipboardPaste' -Value ('"' + $target + '"')

Start-Process $target
Write-Host 'DEPLOY_OK'