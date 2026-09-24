$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$install = Join-Path $env:LOCALAPPDATA 'ChatGPTClipboardFilePaste'
$src = Join-Path $root 'src\ChatGPTClipboardFilePaste.cs'
$candidate = Join-Path $install 'ChatGPTClipboardFilePaste_candidate.exe'
$target = Join-Path $install 'ChatGPTClipboardFilePaste.exe'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
New-Item -ItemType Directory -Force -Path $install | Out-Null
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /out:$candidate `
  /reference:"$wpf\UIAutomationClient.dll" `
  /reference:"$wpf\UIAutomationTypes.dll" `
  /reference:"$wpf\WindowsBase.dll" `
  /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" `
  /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" $src
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Stop-Process -Name ChatGPTClipboardFilePaste -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Copy-Item $candidate $target -Force
Remove-Item $candidate -Force
Start-Process $target
Write-Host 'DEPLOY_OK'