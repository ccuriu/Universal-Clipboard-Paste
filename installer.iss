#define MyAppName "Universal Clipboard Paste"
#define MyAppVersion "1.2.1"
#define MyAppExeName "UniversalClipboardPaste.exe"

[Setup]
AppId={{9B7E07F0-0E58-4FB3-A952-08E9E7F3B77C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Universal Clipboard Paste
DefaultDirName={localappdata}\UniversalClipboardPaste
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=UniversalClipboardPaste-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupLogging=yes

[InstallDelete]
Type: files; Name: "{app}\hotkey.log"
Type: filesandordirs; Name: "{app}\payloads"

[Files]
Source: "dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "UniversalClipboardPaste"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Icons]
Name: "{userprograms}\Universal Clipboard Paste"; Filename: "{app}\{#MyAppExeName}"
Name: "{userprograms}\Uninstall Universal Clipboard Paste"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Universal Clipboard Paste"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM UniversalClipboardPaste.exe /F >nul 2>&1"; Flags: runhidden; RunOnceId: "StopUniversalClipboardPaste"
Filename: "{cmd}"; Parameters: "/C rmdir /S /Q ""%TEMP%\UniversalClipboardPaste"" >nul 2>&1"; Flags: runhidden; RunOnceId: "CleanUniversalClipboardPasteTemp"

[UninstallDelete]
Type: files; Name: "{app}\hotkey.log"

[Code]
procedure StopProcess;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM UniversalClipboardPaste.exe /F >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopProcess;
end;