@echo off
chcp 65001 >nul
rem Hace un respaldo de la base de datos en este momento (clic derecho > Ejecutar como administrador).
net session >nul 2>&1
if errorlevel 1 (
  echo   Haz clic derecho sobre RESPALDAR-AHORA.bat y elige "Ejecutar como administrador".
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0respaldo-mysql.ps1"
echo.
pause
