@echo off
setlocal
title WindBot Duel Log Arena
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-DuelLogArena.ps1" %*
if errorlevel 1 pause
endlocal
