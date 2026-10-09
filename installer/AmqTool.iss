; Inno Setup 6 script for AmqTool. Normally built by build\build-release.ps1, which passes AppVersion and SourceDir.
;
; Installs per user into %LOCALAPPDATA%\Programs\AmqTool: no admin prompt, and AmqTool can write its config.json,
; jobs.json and tmp folder next to the exe and update itself. The old 2021 installer used Program Files (x86),
; which is read-only for normal users and needed the "give Users Full Control" permissions workaround.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\AmqTool"
#endif

[Setup]
; Different from the old per-machine installer's AppId ({20214C4C-4CE1-4852-9AAB-2F913769C32B}) on purpose
AppId={{FA5C5DD9-6F44-4640-9357-A9E69ECC3ABD}
AppName=AmqTool
AppVersion={#AppVersion}
AppVerName=AmqTool v{#AppVersion}
AppPublisher=Kiki
AppPublisherURL=https://github.com/Kikimanox/AmqTool
AppSupportURL=https://github.com/Kikimanox/AmqTool/releases
AppUpdatesURL=https://github.com/Kikimanox/AmqTool/releases
VersionInfoVersion={#AppVersion}
PrivilegesRequired=lowest
DefaultDirName={autopf}\AmqTool
DisableProgramGroupPage=yes
UninstallDisplayName=AmqTool
UninstallDisplayIcon={app}\AmqTool.exe
SetupIconFile=..\AmqTool\music-circle (1).ico
OutputBaseFilename=AmqTool-Setup-v{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Running the setup again is also a way to update, so close a running AmqTool first
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Only app files. config.json, jobs.json etc. are created on first start and never overwritten by updates
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; WorkingDir matters for old builds (they found their config relative to it)
Name: "{autoprograms}\AmqTool"; Filename: "{app}\AmqTool.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\AmqTool"; Filename: "{app}\AmqTool.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AmqTool.exe"; Description: "{cm:LaunchProgram,AmqTool}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; AmqTool's scratch folder. config.json and the job lists are left alone so a reinstall picks them up again
Type: filesandordirs; Name: "{app}\tmp"
