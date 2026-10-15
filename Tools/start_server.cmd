@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0yoo_server.ps1"
echo.
echo server stopped.
pause >nul
