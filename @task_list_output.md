# Task List: NX Configuration Launcher

PRD 기반 개발 작업 목록

---

## 1. 프로젝트 초기 설정

- [x] **1.1 프로젝트 생성**
  - [x] .NET 8 WPF 프로젝트 생성 (`dotnet new wpf -n NXConfigLauncher`)
  - [x] 솔루션 파일 생성
  - [x] .gitignore 파일 추가

- [x] **1.2 프로젝트 구조 설정**
  - [x] 폴더 구조 생성 (Models, ViewModels, Views, Services, Helpers)
  - [x] MVVM 패턴 기본 클래스 구성 (RelayCommand, ViewModelBase)

- [x] **1.3 관리자 권한 설정**
  - [x] app.manifest 파일 생성
  - [x] `requestedExecutionLevel` → `requireAdministrator` 설정
  - [x] 프로젝트 파일에 매니페스트 연결

---

## 2. 핵심 서비스 개발

### 2.1 NX 버전 감지 서비스 (FR-1)

- [x] **2.1.1 NxDetectionService 클래스 생성**
  - [x] 레지스트리 스캔 로직 구현 (`HKLM\SOFTWARE\Siemens\`)
  - [x] 기본 설치 경로 스캔 (`C:\Program Files\Siemens\`) 구현
  - [x] NX 버전 정보 모델 클래스 생성 (`NxVersionInfo`)
    - 속성: VersionName, InstallPath, ExePath
  - [x] 설치된 버전 목록 반환 메서드 구현
  - [x] `ugraf.exe` 존재 여부 검증 로직 추가

### 2.2 방화벽 관리 서비스 (FR-2)

- [x] **2.2.1 FirewallService 클래스 생성**
  - [x] Windows 방화벽 API 연동 (`NetFwTypeLib` COM 참조 또는 `netsh` 명령 사용)
  - [x] Outbound 규칙 추가 메서드 구현
    - 대상: `ugraf.exe`, `lmgrd.exe`, `ugslmd.exe`, `splm*.exe`
  - [x] Outbound 규칙 제거 메서드 구현
  - [x] 규칙 존재 여부 확인 메서드 구현
  - [x] 규칙명 상수 정의 (예: `NXConfigLauncher_Block_ugraf`)

### 2.3 환경변수 관리 서비스 (FR-3, FR-4)

- [x] **2.3.1 EnvironmentService 클래스 생성**
  - [x] `SPLM_LICENSE_SERVER` 읽기/쓰기 메서드 구현
  - [x] 포트@서버주소 파싱 로직 구현
  - [x] 포트 변경 메서드 구현 (서버주소 유지)
  - [x] `UGII_LANG` 읽기/쓰기 메서드 구현
  - [x] 환경변수 변경 시 시스템/사용자 레벨 선택 로직

### 2.4 프로세스 관리 서비스 (FR-5)

- [x] **2.4.1 ProcessService 클래스 생성**
  - [x] 대상 프로세스 목록 상수 정의
    ```
    ugraf.exe, lmgrd.exe, ugslmd.exe, splm*.exe,
    ugs_router.exe, nxsession.exe, nxtask.exe,
    javaw.exe, java.exe, ugtopv.exe
    ```
  - [x] 프로세스 실행 여부 확인 메서드 구현 (`Process.GetProcessesByName`)
  - [x] 전체 프로세스 상태 조회 메서드 구현
  - [x] 특정 프로세스 강제 종료 메서드 구현 (`Process.Kill`)
  - [x] 전체 관련 프로세스 종료 메서드 구현
  - [x] 와일드카드 패턴(`splm*.exe`) 매칭 로직 구현

### 2.5 설정 저장 서비스 (FR-6)

- [x] **2.5.1 ConfigService 클래스 생성**
  - [x] 설정 모델 클래스 생성 (`AppConfig`)
    - 속성: SelectedNxVersion, IsNetworkBlocked, LicensePort, Language
  - [x] JSON 직렬화/역직렬화 구현 (`System.Text.Json`)
  - [x] 설정 파일 경로 결정 (`AppData\Local\NXConfigLauncher\config.json`)
  - [x] 설정 저장 메서드 구현
  - [x] 설정 불러오기 메서드 구현
  - [x] 기본값 설정 로직 구현

### 2.6 NX 실행 서비스

- [x] **2.6.1 NxLauncherService 클래스 생성**
  - [x] NX 실행 메서드 구현 (`Process.Start`)
  - [x] 실행 전 환경변수 적용 로직
  - [x] 실행 전 방화벽 규칙 적용 로직 (옵션에 따라)
  - [x] 실행 결과 반환 (성공/실패)

---

## 3. UI 개발 (WPF)

### 3.1 메인 윈도우 레이아웃

- [x] **3.1.1 MainWindow.xaml 기본 구조**
  - [x] 윈도우 크기, 제목 설정
  - [x] 전체 레이아웃 Grid 구성

- [x] **3.1.2 NX 버전 선택 영역**
  - [x] GroupBox "NX 버전" 생성
  - [x] ComboBox (버전 목록 바인딩)
  - [x] TextBlock (설치 경로 표시)

- [x] **3.1.3 실행 옵션 영역**
  - [x] GroupBox "실행 옵션" 생성
  - [x] CheckBox "네트워크 차단 (방화벽)"
  - [x] ComboBox (라이센스 포트 선택: 28000, 27800, 29000)
  - [x] TextBlock (서버 주소 표시)
  - [x] RadioButton 그룹 (English / Korean)

- [x] **3.1.4 프로세스 상태 영역**
  - [x] GroupBox "프로세스 상태" 생성
  - [x] ItemsControl 또는 WrapPanel로 프로세스 목록 표시
  - [x] 상태 표시 아이콘 (Ellipse: 초록/회색)
  - [x] 새로고침 버튼

- [x] **3.1.5 하단 버튼 영역**
  - [x] "NX 실행" 버튼
  - [x] "프로세스 정리" 버튼

### 3.2 ViewModel 개발

- [x] **3.2.1 MainViewModel 클래스 생성**
  - [x] NX 버전 목록 속성 (`ObservableCollection<NxVersionInfo>`)
  - [x] 선택된 NX 버전 속성
  - [x] 네트워크 차단 여부 속성
  - [x] 라이센스 포트 목록/선택 속성
  - [x] 서버 주소 속성 (읽기 전용)
  - [x] 언어 선택 속성
  - [x] 프로세스 상태 목록 속성 (`ObservableCollection<ProcessStatus>`)

- [x] **3.2.2 Command 구현**
  - [x] LaunchNxCommand (NX 실행)
  - [x] CleanProcessesCommand (프로세스 정리)
  - [x] RefreshProcessStatusCommand (상태 새로고침)

- [x] **3.2.3 초기화 로직**
  - [x] 프로그램 시작 시 NX 버전 감지
  - [x] 저장된 설정 불러오기
  - [x] 프로세스 상태 초기 조회

### 3.3 다이얼로그/팝업

- [x] **3.3.1 확인 다이얼로그**
  - [x] 프로세스 종료 전 확인 팝업 구현 (`MessageBox` 또는 커스텀 다이얼로그)

- [x] **3.3.2 에러 메시지**
  - [x] NX 미설치 시 안내 메시지
  - [x] 관리자 권한 없음 시 안내 메시지
  - [x] 프로세스 종료 실패 시 안내 메시지

---

## 4. 통합 및 테스트

- [x] **4.1 기능 통합**
  - [x] 모든 서비스와 ViewModel 연결
  - [x] 설정 변경 시 자동 저장 로직 연결
  - [x] 프로그램 종료 시 방화벽 규칙 정리 로직

- [x] **4.2 테스트**
  - [x] 빌드 테스트 완료
  - [x] NX 버전 감지 테스트 - 통과 (NX 10 환경에서 정상 인식)
  - [x] 방화벽 규칙 추가/제거 테스트 - 통과 (하드코딩된 프로세스 대상 아웃바운드 차단)
  - [x] 환경변수 변경 테스트 - 통과 (언어/포트 변경 정상 동작)
  - [x] 프로세스 종료 테스트 - 통과
  - [x] 설정 저장/불러오기 테스트 - 통과 (자동 저장/불러오기 방식으로 동작)
  - [x] 관리자 권한 테스트 - 통과 (manifest에서 requireAdministrator 설정됨)

---

## 5. 빌드 및 배포

### 5.1 개발/테스트용 빌드

- [x] **5.1.1 Self-Contained 단일 EXE 빌드**
  - [x] 빌드 스크립트 작성
    ```
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
    ```
  - [x] 빌드 테스트
  - [x] 실행 파일 생성 완료 (약 154MB)

### 5.2 정식 배포용 인스톨러

- [x] **5.2.1 Inno Setup 스크립트 작성**
  - [x] 기본 설치 정보 설정 (앱 이름, 버전, 게시자)
  - [x] 설치 경로 설정 (`C:\Program Files\NX Configuration Launcher\`)
  - [x] 실행 파일 포함
  - [x] 바탕화면/시작 메뉴 바로가기 생성 옵션
  - [x] 프로그램 추가/제거 등록
  - [x] 제거 시 설정 파일 유지 여부 선택 로직

- [x] **5.2.2 인스톨러 빌드 및 테스트**
  - [x] Inno Setup 스크립트 완료 (Installer\setup.iss)
  - [x] 빌드 스크립트 완료 (build.bat)
  - [x] 인스톨러 컴파일 - 완료 (NXConfigLauncher_Setup_1.0.0.exe, 약 44MB)
  - [x] 설치/제거 테스트 - 통과

---

## 6. 문서화

- [x] **6.1 README.md 작성**
  - [x] 프로그램 소개
  - [x] 주요 기능
  - [x] 시스템 요구사항
  - [x] 설치 방법
  - [x] 사용 방법
  - [x] 빌드 방법
  - [x] 프로젝트 구조

---

## 기술 참고사항

| 기능 | 추천 기술/라이브러리 |
|------|---------------------|
| MVVM 패턴 | CommunityToolkit.Mvvm (권장) 또는 직접 구현 |
| JSON 처리 | System.Text.Json (내장) |
| 방화벽 제어 | NetFwTypeLib COM 또는 netsh 명령 |
| 프로세스 관리 | System.Diagnostics.Process |
| 레지스트리 접근 | Microsoft.Win32.Registry |
| 인스톨러 | Inno Setup 6.x |
