@echo off
setlocal
title Demo - Sistema de restaurante
cd /d "%~dp0"

rem ============================================================
rem  Levanta la demo completa en esta PC y la publica por internet
rem  con un tunel temporal de Cloudflare (sin hosting ni cuenta).
rem  Ver GUIA_DEMO.md, seccion 5.
rem ============================================================

where cloudflared >nul 2>nul
if errorlevel 1 (
  echo.
  echo  [!] No se encontro "cloudflared". Instalalo una sola vez con:
  echo.
  echo        winget install --id Cloudflare.cloudflared
  echo.
  echo      Luego cierra esta ventana y vuelve a ejecutar iniciar-demo.bat
  echo.
  pause
  exit /b 1
)

echo.
echo  [1/3] Backend (API .NET en http://localhost:5014)...
start "1 - Backend (API)" /d "%~dp0src\CaraNegra.API" cmd /k dotnet run --launch-profile http

echo  [2/3] Frontend (compila y sirve en http://127.0.0.1:4173)...
start "2 - Frontend (demo)" /d "%~dp0cara-negra-frontend" cmd /k npm run demo

echo  Esperando unos segundos a que arranquen...
timeout /t 15 /nobreak >nul

echo  [3/3] Tunel publico...
start "3 - Tunel (copia la URL https://....trycloudflare.com)" cmd /k cloudflared tunnel --url http://127.0.0.1:4173

echo.
echo  Listo. En la ventana "3 - Tunel" aparece un recuadro con la direccion:
echo.
echo       https://xxxx-xxxx-xxxx.trycloudflare.com
echo.
echo  Abre esa direccion en la Mac, celulares o tablet (desde cualquier red).
echo  La direccion CAMBIA cada vez que se reinicia el tunel.
echo  Para apagar la demo, cierra las 3 ventanas.
echo.
pause
