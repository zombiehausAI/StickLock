; Inno Setup Script for StickLock
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#ifndef MySourceDir
#define MySourceDir "..\artifacts\staging\StickLock"
#endif

[Setup]
AppId={{E58E29A1-D42F-4C96-9815-4D789B84C89A}
AppName=StickLock
AppVersion={#MyAppVersion}
AppPublisher=StickLock Avionics
DefaultDirName={localappdata}\StickLock\app
DefaultGroupName=StickLock
AllowNoIcons=yes
OutputDir=..\artifacts
OutputBaseFilename=StickLock-v{#MyAppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
CloseApplications=yes
CloseApplicationsFilter=StickLock.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\StickLock"; Filename: "{app}\StickLock.exe"
Name: "{group}\{cm:UninstallProgram,StickLock}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\StickLock"; Filename: "{app}\StickLock.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\StickLock.exe"; Description: "{cm:LaunchProgram,StickLock}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\StickLock"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
