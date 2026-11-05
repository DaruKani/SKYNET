@echo off
setlocal EnableExtensions

set "TOOLS_DIR=%~dp0"
set "PAUSE_ON_EXIT=0"
if "%~1"=="" set "PAUSE_ON_EXIT=1"

set "BUILD_ONLY=0"
if /I "%~1"=="--build-only" (
    set "BUILD_ONLY=1"
    shift
)

set "HOST=%~1"
if not defined HOST set "HOST=127.0.0.1"

set "PORT=%~2"
if not defined PORT set "PORT=7911"

set "DB_PATH=%~3"
if not defined DB_PATH set "DB_PATH=D:\Games\MDPro4\Data\locales\en-US\cards.cdb"

set "HOST_INFO=%~4"
set "WINDBOT_ROOT=%TOOLS_DIR%.."
set "SOLUTION=%WINDBOT_ROOT%\WindBot.sln"
set "OUTPUT_DIR=%WINDBOT_ROOT%\bin\Debug"
set "BOT_EXE=%OUTPUT_DIR%\WindBot.exe"
set "LOG_STAMP=%RANDOM%"
for /f "delims=" %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "LOG_STAMP=%%I"
set "LOG_PATH=%TOOLS_DIR%SkyStriker2026-%LOG_STAMP%.log"

set "MSBUILD="
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD for /f "delims=" %%I in ('where msbuild.exe 2^>nul') do if not defined MSBUILD set "MSBUILD=%%I"

if not exist "%SOLUTION%" (
    echo ERROR: WindBot.sln was not found at "%SOLUTION%".
    goto :failure
)

if not defined MSBUILD (
    echo ERROR: MSBuild was not found. Install Visual Studio 2022 or Build Tools with .NET desktop build tools.
    goto :failure
)

echo Building SkyStriker2026 WindBot in Debug mode...
"%MSBUILD%" "%SOLUTION%" /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
if errorlevel 1 (
    echo ERROR: The Debug build failed.
    goto :failure
)

if "%BUILD_ONLY%"=="1" (
    echo Build completed: "%BOT_EXE%"
    exit /b 0
)

if not exist "%DB_PATH%" (
    echo ERROR: cards.cdb was not found at "%DB_PATH%".
    echo Pass its path as the third argument.
    goto :failure
)

if not exist "%BOT_EXE%" (
    echo ERROR: WindBot.exe was not produced at "%BOT_EXE%".
    goto :failure
)

echo.
echo Joining MDPro4 at %HOST%:%PORT% as SkyStriker2026...
echo Open the compatible MDPro4 room before this connection attempt.
echo Decision log: "%LOG_PATH%"
pushd "%OUTPUT_DIR%"
if errorlevel 1 goto :failure

if defined HOST_INFO (
    WindBot.exe Name=SkyStriker2026 Deck=SkyStriker2026 Dialog=Universal_3.EN "DbPath=%DB_PATH%" Host=%HOST% Port=%PORT% "HostInfo=%HOST_INFO%" Debug=True "LogPath=%LOG_PATH%"
) else (
    WindBot.exe Name=SkyStriker2026 Deck=SkyStriker2026 Dialog=Universal_3.EN "DbPath=%DB_PATH%" Host=%HOST% Port=%PORT% Debug=True "LogPath=%LOG_PATH%"
)
set "BOT_EXIT=%ERRORLEVEL%"
popd

echo WindBot exited with code %BOT_EXIT%.
if "%PAUSE_ON_EXIT%"=="1" pause
exit /b %BOT_EXIT%

:failure
if "%PAUSE_ON_EXIT%"=="1" pause
exit /b 1
