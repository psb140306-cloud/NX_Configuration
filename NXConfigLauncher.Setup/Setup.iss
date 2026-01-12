; NX Configuration Launcher - Inno Setup Script
;
; 설치 전에 다음을 수행하세요:
; 1. Inno Setup을 다운로드: https://jrsoftware.org/isdl.php
; 2. 이 스크립트를 Inno Setup Compiler로 컴파일

#define MyAppName "NX Configuration Launcher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "NX Config Team"
#define MyAppExeName "NXConfigLauncher.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".nxconfig"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; 앱 기본 정보
AppId={{B7F8C3D2-A1E5-4F9B-8D6C-2E3F4A5B6C7D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\Output
OutputBaseFilename=NXConfigLauncherSetup
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 발행된 모든 파일 포함
Source: "..\NXConfigLauncher\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 제거 시 방화벽 규칙 제거 (관리자 권한으로 실행)
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""NX_OUTBOUND_BLOCK"""; Flags: runhidden

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // 설치 완료 후 추가 작업이 필요하면 여기에 추가
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // hosts 파일 정리 - PowerShell 스크립트 실행
    Exec('powershell.exe',
         '-NoProfile -ExecutionPolicy Bypass -Command "' +
         '(Get-Content C:\Windows\System32\drivers\etc\hosts) | ' +
         'Where-Object { $_ -notmatch ''# NX_CONFIG_BLOCK'' } | ' +
         'Set-Content C:\Windows\System32\drivers\etc\hosts"',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
