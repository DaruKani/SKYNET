@echo off
title Skynet Arena
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-SkynetArena.ps1"
if errorlevel 1 pause
