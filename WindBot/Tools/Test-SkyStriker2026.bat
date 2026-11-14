@echo off
setlocal EnableExtensions
set "SKY_PYTHON=python"
if exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" set "SKY_PYTHON=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-SkyStriker2026.ps1" -Python "%SKY_PYTHON%" %*
set "SKY_RESULT=%ERRORLEVEL%"
if not "%SKY_RESULT%"=="0" echo FAILED: inspect the reported receipt directory.
pause
exit /b %SKY_RESULT%
