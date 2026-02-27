; ============================================================
;  SchoolSchedule -- Inno Setup Installer Script
;  Файл: installer/SchoolSchedule.iss
; ============================================================

#define AppName    "SchoolSchedule"
#define AppVersion "1.0.0"
#define AppPublisher "RENDERGAMES"
#define AppURL     "https://github.com/MihailKashintsev/SchoolSchedule"
#define AppExeName "Kiosk.exe"
#define AppId      "{8B3F4A2C-1D5E-4F6B-9C3A-7E2D8F1B4C5A}"

[Setup]
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=SchoolSchedule-Setup-v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
MinVersion=6.1sp1
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=yes
WizardStyle=modern
WizardSizePercent=120

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon";  Description: "Create desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked
Name: "startupentry"; Description: "Run on Windows startup";  GroupDescription: "Additional tasks:"; Flags: unchecked

[Files]
; publish/ папка (генерируется dotnet publish в CI или локально)
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*.dll";         DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\publish\*.json";        DestDir: "{app}"; Flags: ignoreversion; Check: FileExists('..\publish\*.json')
Source: "..\publish\*.config";      DestDir: "{app}"; Flags: ignoreversion

; Ресурсы
Source: "..\Images\*"; DestDir: "{app}\Images"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists('..\Images')
Source: "..\Maps\*";   DestDir: "{app}\Maps";   Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists('..\Maps')

[Icons]
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  Exec('taskkill.exe', '/F /IM {#AppExeName}', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;

