#define MyAppName "每日计划"
#define MyAppVersion "1.1.3"
#define MyAppExeName "DailyPlanNote.exe"
#define OutputRoot "..\bin"

[Setup]
AppId={{FEA54300-0188-4B51-95D8-56FD8D1BE37C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={localappdata}\Programs\DailyPlanNote
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputRoot}
OutputBaseFilename=DailyPlanNote-Setup
SetupIconFile=..\assets\note.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
MinVersion=10.0
; Restart Manager can close a running copy after the user confirms the upgrade.
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter={#MyAppExeName}
VersionInfoVersion=1.1.3.0
VersionInfoDescription=每日计划 安装程序
VersionInfoProductName=每日计划

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#OutputRoot}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'DailyPlanNote';

// 仅当用户已经开启开机自启时，把原路径迁移到安装目录。
procedure CurStepChanged(CurStep: TSetupStep);
var
  OldPath: string;
begin
  if CurStep = ssPostInstall then
    if RegQueryStringValue(HKEY_CURRENT_USER, RunKey, RunValue, OldPath) and (OldPath <> '') then
      RegWriteStringValue(HKEY_CURRENT_USER, RunKey, RunValue,
        '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"');
end;

// 卸载只清理仍指向本次安装目录的自启项，不碰用户修改过的其他路径。
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentPath, InstalledPath: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    InstalledPath := '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"';
    if RegQueryStringValue(HKEY_CURRENT_USER, RunKey, RunValue, CurrentPath) and
       (CompareText(CurrentPath, InstalledPath) = 0) then
      RegDeleteValue(HKEY_CURRENT_USER, RunKey, RunValue);
  end;
end;
