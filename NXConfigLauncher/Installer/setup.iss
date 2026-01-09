; NX Configuration Launcher - Inno Setup Script
; 이 스크립트는 Inno Setup 6.x 이상에서 컴파일 가능합니다.

#define MyAppName "NX Configuration Launcher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "NX Config"
#define MyAppExeName "NXConfigLauncher.exe"
#define MyAppURL "https://github.com/nxconfig"

[Setup]
; 기본 설정
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 설치 경로
DefaultDirName={autopf64}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; 출력 설정
OutputDir=..\bin\Installer
OutputBaseFilename=NXConfigLauncher_Setup_{#MyAppVersion}
SetupIconFile=..\Assets\N_Gear.ico
Compression=lzma2
SolidCompression=yes

; 권한 설정 (관리자 권한 필요)
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; UI 설정
WizardStyle=modern
WizardResizable=no

; 기타
LicenseFile=
InfoBeforeFile=
InfoAfterFile=

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; publish 폴더의 모든 파일 포함 (self-contained 배포)
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 시작 메뉴 바로가기
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; 바탕화면 바로가기
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; 빠른 실행 바로가기
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; 설치 완료 후 실행 옵션
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
; 제거 시 삭제할 파일/폴더 (설정 파일은 유지)
Type: filesandordirs; Name: "{app}\logs"

[Code]
// 설정 파일 유지 여부 확인
function InitializeUninstall(): Boolean;
var
  ConfigPath: String;
  MsgResult: Integer;
begin
  Result := True;

  ConfigPath := ExpandConstant('{localappdata}\NXConfigLauncher\config.json');

  if FileExists(ConfigPath) then
  begin
    MsgResult := MsgBox('사용자 설정 파일을 유지하시겠습니까?' + #13#10 +
                        '(다음 경로에 저장됨: ' + ConfigPath + ')',
                        mbConfirmation, MB_YESNO);

    if MsgResult = IDNO then
    begin
      DeleteFile(ConfigPath);
      RemoveDir(ExpandConstant('{localappdata}\NXConfigLauncher'));
    end;
  end;
end;

// 이전 버전 제거 확인
function InitializeSetup(): Boolean;
var
  UninstallKey: String;
  UninstallString: String;
  ResultCode: Integer;
begin
  Result := True;

  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1';

  if RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', UninstallString) or
     RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString) then
  begin
    if MsgBox('이전 버전이 설치되어 있습니다. 제거하고 계속하시겠습니까?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec(RemoveQuotes(UninstallString), '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    end
    else
    begin
      Result := False;
    end;
  end;
end;
