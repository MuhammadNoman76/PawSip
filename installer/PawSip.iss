; PawSip installer (Inno Setup 6) - built by build.ps1 from ..\publish

#define AppName "PawSip"
#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#define AppExe "PawSip.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{B7D2F4A1-3C8E-4F6B-9A21-5E7C0D9A4B13}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Muhammad Noman
AppPublisherURL=https://github.com/MuhammadNoman76/PawSip
AppComments=A cute cat that reminds you to drink water
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=PawSip-Setup-{#AppVersion}
SetupIconFile=..\src\PawSip\Assets\PawSip.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Installs for the current user only: no admin prompt, starts with this user's Windows session.
PrivilegesRequired=lowest
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start {#AppName} with Windows (recommended, runs quietly in the tray)"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Removed on uninstall (the app itself writes it when "Start with Windows" is on).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--set-startup on"; Flags: runhidden waituntilterminated; Tasks: startup
Filename: "{app}\{#AppExe}"; Parameters: "--set-startup off"; Flags: runhidden waituntilterminated; Tasks: not startup
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopPawSip();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopPawSip();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopPawSip();
  Result := True;
end;
