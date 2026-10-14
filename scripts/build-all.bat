@echo off
title Checkmate - build for Windows + Linux + macOS
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-all.ps1" %*
echo.
pause
