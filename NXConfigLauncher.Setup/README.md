# NX Configuration Launcher 설치 프로그램 만들기

## 방법 1: Inno Setup 사용 (권장)

### 1단계: Inno Setup 다운로드 및 설치
1. https://jrsoftware.org/isdl.php 에서 Inno Setup 다운로드
2. Inno Setup 설치 (무료)

### 2단계: 애플리케이션 발행
PowerShell 또는 명령 프롬프트에서 실행:
```bash
cd d:\startcoding\NX_Configuration\NXConfigLauncher
dotnet publish -c Release -r win-x64 --self-contained false
```

### 3단계: 설치 프로그램 컴파일
1. Inno Setup Compiler 실행
2. `Setup.iss` 파일 열기
3. Build > Compile 메뉴 선택
4. `Output` 폴더에 `NXConfigLauncherSetup.exe` 생성됨

### 4단계: 설치 프로그램 실행
- `NXConfigLauncherSetup.exe`를 관리자 권한으로 실행
- 설치 마법사 따라 진행
- 프로그램 추가/제거에서 관리 가능

---

## 방법 2: WiX Toolset 사용 (고급)

### 1단계: WiX Toolset 설치
1. https://wixtoolset.org/releases/ 에서 WiX v5 다운로드
2. WiX 설치

### 2단계: MSI 빌드
PowerShell에서 실행:
```bash
cd d:\startcoding\NX_Configuration\NXConfigLauncher.Setup
dotnet build NXConfigLauncher.Setup.wixproj
```

---

## 방법 3: 수동 배포 (가장 간단)

### Portable 버전 만들기
1. 발행된 파일을 ZIP으로 압축:
```bash
cd d:\startcoding\NX_Configuration\NXConfigLauncher\bin\Release\net8.0-windows\win-x64\publish
Compress-Archive -Path * -DestinationPath NXConfigLauncher-Portable.zip
```

2. ZIP 파일을 원하는 위치에 압축 해제
3. 관리자 권한으로 `NXConfigLauncher.exe` 실행

---

## 설치 프로그램 기능

### 설치 시
- Program Files에 애플리케이션 설치
- 시작 메뉴에 바로가기 생성
- 선택 시 바탕화면 아이콘 생성
- 관리자 권한 요구

### 제거 시
- 모든 파일 삭제
- 방화벽 규칙 자동 제거
- hosts 파일에서 차단 도메인 제거
- 레지스트리 정리

---

## 문제 해결

### Inno Setup 컴파일 오류
- 발행(publish) 폴더가 비어있는지 확인
- 경로에 한글이 없는지 확인
- 관리자 권한으로 Inno Setup 실행

### 설치 중 오류
- 이전 버전 완전히 제거 후 재설치
- 관리자 권한으로 설치 프로그램 실행
- 백신 프로그램 일시 비활성화

### 실행 오류
- .NET 8.0 Runtime 설치 확인
- 관리자 권한으로 실행
- Windows 방화벽 허용 확인
