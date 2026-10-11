; PawSip installer (Inno Setup 6) - built by build.ps1, once per edition:
;   /DEdition=x64     Windows 10/11, 64-bit Intel/AMD (also runs on ARM64 via emulation)
;   /DEdition=arm64   Windows 10/11 on ARM (Snapdragon, Surface Pro X...)
;   /DEdition=x86     Windows 10/11, 32-bit
;   /DEdition=legacy  Windows 7 SP1 / 8 / 8.1 (and newer), needs .NET Framework 4.8

#define AppName "PawSip"
#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#ifndef Edition
  #define Edition "x64"
#endif
#define AppExe "PawSip.exe"
#define PublishDir "..\publish\" + Edition

[Setup]
; Same AppId for every edition: installing another edition simply replaces the current one.
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
#if Edition == "legacy"
OutputBaseFilename=PawSip-Setup-{#AppVersion}-win7-8
#else
OutputBaseFilename=PawSip-Setup-{#AppVersion}-{#Edition}
#endif
SetupIconFile=..\src\PawSip\Assets\PawSip.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
#if Edition == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
#elif Edition == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
MinVersion=10.0
#elif Edition == "x86"
MinVersion=10.0
#else
MinVersion=6.1sp1
#endif
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

#if Edition == "legacy"
function HasDotNet48(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not HasDotNet48() then
  begin
    if MsgBox('PawSip for Windows 7 / 8 needs Microsoft .NET Framework 4.8, which is not installed on this PC.' + #13#10#13#10 +
              'Open Microsoft''s download page now? Install it, restart if asked, then run this setup again.',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
#endif

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
