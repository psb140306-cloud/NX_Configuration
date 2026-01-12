# NX Configuration Launcher 설치 프로그램 빌드 스크립트

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("InnoSetup", "ZIP", "Both")]
    [string]$BuildType = "Both"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $ScriptDir
$ProjectDir = Join-Path $SolutionDir "NXConfigLauncher"
$PublishDir = Join-Path $ProjectDir "bin\Release\net8.0-windows\win-x64\publish"
$OutputDir = Join-Path $SolutionDir "Output"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "NX Configuration Launcher 설치 빌드" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 출력 디렉토리 생성
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "✓ 출력 디렉토리 생성: $OutputDir" -ForegroundColor Green
}

# 1단계: 애플리케이션 발행
Write-Host ""
Write-Host "[1/3] 애플리케이션 발행 중..." -ForegroundColor Yellow
Push-Location $ProjectDir
try {
    dotnet publish -c Release -r win-x64 --self-contained false
    if ($LASTEXITCODE -ne 0) {
        throw "발행 실패"
    }
    Write-Host "✓ 발행 완료: $PublishDir" -ForegroundColor Green
}
finally {
    Pop-Location
}

# ZIP 파일 생성
if ($BuildType -eq "ZIP" -or $BuildType -eq "Both") {
    Write-Host ""
    Write-Host "[2/3] Portable ZIP 생성 중..." -ForegroundColor Yellow

    $ZipPath = Join-Path $OutputDir "NXConfigLauncher-Portable-v1.0.0.zip"
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Push-Location $PublishDir
    try {
        Compress-Archive -Path * -DestinationPath $ZipPath
        Write-Host "✓ ZIP 생성 완료: $ZipPath" -ForegroundColor Green

        $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
        Write-Host "  파일 크기: $ZipSize MB" -ForegroundColor Gray
    }
    finally {
        Pop-Location
    }
}

# Inno Setup 컴파일
if ($BuildType -eq "InnoSetup" -or $BuildType -eq "Both") {
    Write-Host ""
    Write-Host "[3/3] Inno Setup 설치 프로그램 생성 중..." -ForegroundColor Yellow

    # Inno Setup 경로 찾기
    $InnoSetupPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )

    $ISCC = $null
    foreach ($path in $InnoSetupPaths) {
        if (Test-Path $path) {
            $ISCC = $path
            break
        }
    }

    if ($null -eq $ISCC) {
        Write-Host "⚠ Inno Setup이 설치되지 않았습니다." -ForegroundColor Yellow
        Write-Host "  다운로드: https://jrsoftware.org/isdl.php" -ForegroundColor Gray
        Write-Host "  ZIP 파일은 생성되었습니다." -ForegroundColor Gray
    }
    else {
        $IssPath = Join-Path $ScriptDir "Setup.iss"
        & $ISCC $IssPath /Q

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Inno Setup 설치 프로그램 생성 완료" -ForegroundColor Green

            $SetupPath = Join-Path $OutputDir "NXConfigLauncherSetup.exe"
            if (Test-Path $SetupPath) {
                $SetupSize = [math]::Round((Get-Item $SetupPath).Length / 1MB, 2)
                Write-Host "  파일: $SetupPath" -ForegroundColor Gray
                Write-Host "  크기: $SetupSize MB" -ForegroundColor Gray
            }
        }
        else {
            Write-Host "✗ Inno Setup 컴파일 실패" -ForegroundColor Red
        }
    }
}

# 완료 메시지
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "빌드 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "출력 디렉토리: $OutputDir" -ForegroundColor White
Write-Host ""

# 탐색기에서 출력 폴더 열기
Start-Process explorer.exe -ArgumentList $OutputDir
