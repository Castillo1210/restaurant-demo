@echo off
chcp 65001 >nul
rem Ejecutar con clic derecho > "Ejecutar como administrador"
net session >nul 2>&1
if errorlevel 1 (
  echo.
  echo   Este programa necesita permisos de ADMINISTRADOR.
  echo   Cierra esta ventana, haz clic derecho sobre INSTALAR.bat y elige "Ejecutar como administrador".
  echo.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0instalar.ps1" %*
echo.
pause
