@echo off
title Checkmate - Windows build
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-windows.ps1" %*
echo.
pause
