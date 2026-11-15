@echo off
setlocal
title Shaddoll 2025 Arena
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Shaddoll2025Arena.ps1" %*
if errorlevel 1 pause
endlocal
