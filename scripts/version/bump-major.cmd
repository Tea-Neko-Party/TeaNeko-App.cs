@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0version.ps1" bump major
pause
