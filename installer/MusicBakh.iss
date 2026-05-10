; Inno Setup-скрипт MusicBakh.
; Версия передаётся параметром: ISCC /DMyAppVersion=1.0.0 MusicBakh.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppName "MusicBakh"
#define MyAppPublisher "Matvey Bakhmatov"
#define MyAppExeName "MusicBakh.exe"
#define MyAppURL "https://github.com/Matvey-Bakhmatov/MusicBakh"

[Setup]
; Уникальный AppId — менять нельзя, иначе обновление поверх установится как новое приложение.
AppId={{60115D0E-86AA-4D7A-A559-D8D70C0B6C16}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=Copyright (c) 2026 {#MyAppPublisher}. Apache-2.0.
VersionInfoDescription={#MyAppName} installer
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile=..\MusicLibrary\Assets\Brand\musicbakh.ico
LicenseFile=..\LICENSE
OutputDir=..\release
OutputBaseFilename=MusicBakh-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.17763
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\MusicBakh.exe"; DestDir: "{app}"; Flags: ignoreversion
; Эталонные треки и обложки кладём рядом с .exe — приложение ищет их в AppContext.BaseDirectory.
Source: "..\publish\win-x64\Music\*"; DestDir: "{app}\Music"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\win-x64\Covers\*"; DestDir: "{app}\Covers"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Чистим только то, что положил установщик. Пользовательские данные в
; %LocalAppData%\MusicLibrary\ и %LocalAppData%\MusicBakh\ не трогаем.
Type: filesandordirs; Name: "{app}\Music"
Type: filesandordirs; Name: "{app}\Covers"
