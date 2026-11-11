; =============================================================================
; Ivory Island — Official Windows 10 (22H2 Build 19045) & Windows 11 Installer
; Output: Release\IvoryIsland-Setup.exe
; =============================================================================

#define MyAppName "Ivory Island"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ivory Island Engineering"
#define MyAppExeName "IvoryIsland.exe"
#define PortableSourceDir "..\Release\IvoryIsland-Portable"

[Setup]
AppId={{8A2F9B4E-51D2-4C8E-9E3A-1D7B6C5A4F01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) 2026 {#MyAppPublisher}

; Default per-user installation path: %LOCALAPPDATA%\Programs\IvoryIsland
DefaultDirName={localappdata}\Programs\IvoryIsland
DefaultGroupName={#MyAppName}

; Enable directory Browse selection page so users can install to any folder or drive (e.g. D:\Applications\IvoryIsland)
DisableDirPage=no
DisableWelcomePage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableFinishedPage=no
DirExistsWarning=no
EnableDirDoesntExistWarning=no
UsePreviousAppDir=yes

; Standard-user installation without mandatory UAC elevation
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Target 64-bit Windows 10 (1809 Build 17763 / 22H2 Build 19045) and Windows 11 x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Branding & Visual Identity
WizardStyle=modern
SetupIconFile=..\src\IvoryIsland\Assets\IvoryIsland.ico
WizardImageFile=..\src\IvoryIsland\Assets\WizardImage.bmp
WizardSmallImageFile=..\src\IvoryIsland\Assets\WizardSmallImage.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Generate exactly Release\IvoryIsland-Setup.exe
OutputDir=..\Release
OutputBaseFilename=IvoryIsland-Setup
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start {#MyAppName} automatically when Windows signs in"; GroupDescription: "Startup Options:"

[Files]
; Package all self-contained portable runtime outputs and icon assets
Source: "{#PortableSourceDir}\*"; DestDir: "{app}"; Excludes: ".gitkeep"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\IvoryIsland\Assets\IvoryIsland.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "..\src\IvoryIsland\Assets\IvoryIsland.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
; Start Menu shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\IvoryIsland.ico"; Comment: "Minimal Ivory Liquid Glass Floating Media Controller"
; Optional Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\IvoryIsland.ico"; Tasks: desktopicon; Comment: "Minimal Ivory Liquid Glass Floating Media Controller"

[Registry]
; Optional per-user startup entry (removed on uninstall, while %LOCALAPPDATA%\IvoryIsland user data is strictly preserved)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "IvoryIsland"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Launch Ivory Island after installation finishes
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
// Stop any running IvoryIsland.exe instance cleanly before install/upgrade/uninstall.
// Note: %LOCALAPPDATA%\IvoryIsland user settings and cache are NEVER deleted during uninstallation.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM IvoryIsland.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM IvoryIsland.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
