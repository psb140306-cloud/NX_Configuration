@echo off
echo ==========================================
echo NX Configuration Launcher Build Script
echo ==========================================
echo.

:: 빌드 디렉토리 설정
set BUILD_DIR=%~dp0

:: Step 1: Clean
echo [1/3] Cleaning previous build...
dotnet clean -c Release
if errorlevel 1 goto error

:: Step 2: Publish
echo.
echo [2/3] Publishing Self-Contained EXE...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 goto error

:: Step 3: Inno Setup (옵션)
echo.
echo [3/3] Creating Installer...
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%BUILD_DIR%Installer\setup.iss"
    if errorlevel 1 goto error
) else (
    echo Inno Setup not found. Skipping installer creation.
    echo Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php
)

echo.
echo ==========================================
echo Build completed successfully!
echo ==========================================
echo.
echo Output files:
echo   - EXE: bin\Release\net8.0-windows\win-x64\publish\NXConfigLauncher.exe
echo   - Installer: bin\Installer\NXConfigLauncher_Setup_1.0.0.exe (if Inno Setup installed)
echo.
goto end

:error
echo.
echo ==========================================
echo Build failed!
echo ==========================================
exit /b 1

:end
pause
