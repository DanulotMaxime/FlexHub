#define AppName "Personal Apps Hub"
#define AppExeName "PersonalAppsHub.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\installer\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{CB117D29-B25D-4CC6-9415-8E68C73D6241}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Flexron
AppContact=Discord : Flexron
DefaultDirName={localappdata}\Programs\PersonalAppsHub
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=PersonalAppsHub-Setup-{#AppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription=Installeur de {#AppName}
VersionInfoCompany=Flexron

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked
Name: "startup"; Description: "Lancer automatiquement avec Windows"; GroupDescription: "Démarrage :"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PersonalAppsHub"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Lancer {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExeName} /F"; Flags: runhidden; RunOnceId: "StopPersonalAppsHub"
