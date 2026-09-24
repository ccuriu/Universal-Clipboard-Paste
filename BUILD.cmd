@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:dist\ChatGPTClipboardFilePaste.exe /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" src\ChatGPTClipboardFilePaste.cs
if errorlevel 1 exit /b 1
echo BUILD_OK