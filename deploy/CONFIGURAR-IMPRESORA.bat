@echo off
chcp 65001 >nul
rem Configura o cambia la ticketera de COCINA (comandas): clic derecho > Ejecutar como administrador.
net session >nul 2>&1
if errorlevel 1 (
  echo   Haz clic derecho sobre CONFIGURAR-IMPRESORA.bat y elige "Ejecutar como administrador".
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configurar-impresora.ps1" %*
echo.
pause
