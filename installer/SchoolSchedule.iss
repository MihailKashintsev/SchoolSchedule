; ============================================================
;  SchoolSchedule — Inno Setup Installer Script
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
; Путь к иконке (относительно корня репозитория)
SetupIconFile=..\Icon_32_32.ico
; Вывод установщика
OutputDir=Output
OutputBaseFilename=SchoolSchedule-Setup-v{#AppVersion}
; Сжатие
Compression=lzma2/ultra64
SolidCompression=yes
; Требуем .NET 4.8
MinVersion=6.1sp1
; Разрешаем установку без прав администратора
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Закрываем приложение при обновлении
CloseApplications=yes
RestartApplications=yes
; Вид установщика
WizardStyle=modern
WizardSizePercent=120

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon";    Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные задачи:"; Flags: unchecked
Name: "startupentry";   Description: "Запускать при входе в систему";  GroupDescription: "Дополнительные задачи:"; Flags: unchecked

[Files]
; Основной exe
Source: "..\bin\Release\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Все DLL зависимости
Source: "..\bin\Release\publish\*.dll";    DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Config файлы
Source: "..\bin\Release\publish\*.config"; DestDir: "{app}"; Flags: ignoreversion

; Ресурсы — изображения
Source: "..\Images\*"; DestDir: "{app}\Images"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists('..\Images')

; Ресурсы — карты
Source: "..\Maps\*"; DestDir: "{app}\Maps";   Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists('..\Maps')

; Дефолтный конфиг настроек (НЕ перезаписываем если уже есть)
Source: "..\settings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist; Check: FileExists('..\settings.json')

[Icons]
; Меню Пуск
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Удалить {#AppName}";   Filename: "{uninstallexe}"

; Рабочий стол
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Автозапуск
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; Запустить после установки
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Удалять только папки приложения, не трогаем пользовательские данные
Type: filesandordirs; Name: "{app}\Images"
Type: filesandordirs; Name: "{app}\Maps"

[Code]
// ── Проверка .NET Framework 4.8 ────────────────────────────────────────────
function IsDotNetInstalled: Boolean;
var
  Version: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release', Version) then
  begin
    // 528040 = .NET 4.8
    Result := (Version >= 528040);
  end;
end;

function InitializeSetup: Boolean;
begin
  if not IsDotNetInstalled then
  begin
    MsgBox(
      '.NET Framework 4.8 не найден.' + #13#10 +
      'Пожалуйста, установите его с сайта Microsoft перед установкой {#AppName}.' + #13#10#13#10 +
      'https://dotnet.microsoft.com/download/dotnet-framework/net48',
      mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;

// ── Закрыть приложение перед обновлением ───────────────────────────────────
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Убиваем процесс если запущен
  Exec('taskkill.exe', '/F /IM {#AppExeName}', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;
