# NX Configuration Launcher

NX 프로그램 실행 전 설정을 간편하게 관리하고, 종료 후 잔여 프로세스를 정리하는 런처 프로그램입니다.

## 주요 기능

### 1. NX 버전 자동 감지 및 선택 실행
- 시스템에 설치된 NX 버전을 자동으로 감지
- 여러 버전이 설치된 경우 원하는 버전 선택 가능
- 설치 경로 표시

### 2. 네트워크 차단 (방화벽)
- NX 실행 시 관련 프로세스의 네트워크 통신 차단
- Windows 방화벽 Outbound 규칙 자동 추가/제거
- 차단 대상: `ugraf.exe`, `lmgrd.exe`, `ugslmd.exe`, `splm*.exe`

### 3. 라이센스 서버 포트 변경
- `SPLM_LICENSE_SERVER` 환경변수 설정
- 사용 가능한 포트: 28000, 27800, 29000
- 서버 주소는 기존 설정 유지

### 4. 언어 변경
- `UGII_LANG` 환경변수 설정
- 지원 언어: English, Korean

### 5. 프로세스 모니터링 및 정리
- NX 관련 프로세스 실행 상태 실시간 표시
- 잔여 프로세스 일괄 종료 기능
- 대상 프로세스:
  - `ugraf.exe`, `lmgrd.exe`, `ugslmd.exe`, `splm*.exe`
  - `ugs_router.exe`, `nxsession.exe`, `nxtask.exe`
  - `javaw.exe`, `java.exe`, `ugtopv.exe`

### 6. 설정 자동 저장
- 모든 설정은 자동으로 저장되어 다음 실행 시 유지
- 저장 위치: `%LocalAppData%\NXConfigLauncher\config.json`

## 시스템 요구사항

- **운영체제**: Windows 10/11 (64-bit)
- **권한**: 관리자 권한 필요 (방화벽 규칙 추가/제거를 위해)
- **NX**: Siemens NX가 설치되어 있어야 함

## 설치 방법

### 방법 1: 인스톨러 사용 (권장)
1. `NXConfigLauncher_Setup_x.x.x.exe` 실행
2. 설치 마법사 지시에 따라 설치
3. 바탕화면 또는 시작 메뉴에서 실행

### 방법 2: 단일 실행 파일 사용
1. `NXConfigLauncher.exe` 파일을 원하는 위치에 복사
2. 관리자 권한으로 실행

## 사용 방법

1. **프로그램 실행**: 관리자 권한으로 NX Configuration Launcher 실행
2. **NX 버전 선택**: 드롭다운에서 실행할 NX 버전 선택
3. **옵션 설정**:
   - 네트워크 차단이 필요하면 체크박스 선택
   - 라이센스 포트 선택
   - 언어 선택 (English/Korean)
4. **NX 실행**: "NX 실행" 버튼 클릭
5. **프로세스 정리**: NX 종료 후 잔여 프로세스가 있으면 "프로세스 정리" 버튼 클릭

## 빌드 방법

### 요구사항
- .NET 8.0 SDK
- (선택) Inno Setup 6.x (인스톨러 생성용)

### 빌드 명령

```bash
# Debug 빌드
dotnet build

# Release 빌드 (Self-Contained 단일 EXE)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 또는 빌드 스크립트 사용
build.bat
```

### 출력 파일
- **EXE**: `bin\Release\net8.0-windows\win-x64\publish\NXConfigLauncher.exe`
- **인스톨러**: `bin\Installer\NXConfigLauncher_Setup_1.0.0.exe` (Inno Setup 설치 시)

## 프로젝트 구조

```
NXConfigLauncher/
├── Helpers/
│   ├── ViewModelBase.cs      # MVVM 기본 클래스
│   ├── RelayCommand.cs       # Command 구현
│   └── BoolToColorConverter.cs
├── Models/
│   ├── NxVersionInfo.cs      # NX 버전 정보
│   ├── ProcessStatus.cs      # 프로세스 상태
│   └── AppConfig.cs          # 앱 설정
├── Services/
│   ├── NxDetectionService.cs # NX 버전 감지
│   ├── FirewallService.cs    # 방화벽 관리
│   ├── EnvironmentService.cs # 환경변수 관리
│   ├── ProcessService.cs     # 프로세스 관리
│   ├── ConfigService.cs      # 설정 저장/불러오기
│   └── NxLauncherService.cs  # NX 실행
├── ViewModels/
│   └── MainViewModel.cs      # 메인 ViewModel
├── Installer/
│   └── setup.iss             # Inno Setup 스크립트
├── MainWindow.xaml           # 메인 UI
├── MainWindow.xaml.cs
├── App.xaml
├── app.manifest              # 관리자 권한 설정
└── build.bat                 # 빌드 스크립트
```

## 라이선스

MIT License

## 문의

문제가 발생하거나 기능 요청이 있으시면 이슈를 등록해 주세요.
