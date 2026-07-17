#define MyAppName "白荆科技宿舍走廊"
#define MyAppEnglishName "SeedDormitoryCorridor"
#define MyAppExeName "SeedDormitoryCorridor.App.exe"
#define MyAppVersion "0.1.0"

[Setup]
AppId={{51D63DD5-5E96-4A60-AC56-BA303DDAB7E1}
AppName={#MyAppName}
AppVerName={#MyAppName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher=SeedDormitoryCorridor contributors
DefaultDirName={localappdata}\Programs\{#MyAppEnglishName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=SeedDormitoryCorridor-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
AppMutex=Local\SeedDormitoryCorridor-51D63DD5-5E96-4A60-AC56-BA303DDAB7E1
LicenseFile=..\LICENSE

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('是否同时删除用户配置、日志和已安装的宠物？' + #13#10 +
              '选择“否”将保留这些数据，便于以后重新安装。',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\SeedDormitoryCorridor'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\SeedDormitoryCorridor'), True, True, True);
    end;
  end;
end;
