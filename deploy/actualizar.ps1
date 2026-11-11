# =====================================================================
#  actualizar.ps1 - Instala una VERSIÓN NUEVA del Restaurante POS sobre
#  una instalación existente, conservando configuración, datos y logs.
#
#  Ejecutar como ADMINISTRADOR desde la carpeta del paquete NUEVO
#  (ACTUALIZAR.bat, clic derecho > Ejecutar como administrador).
#
#  Pasos: respaldo de la base > detener servicio > guardar copia de la
#  versión actual > copiar la nueva > actualizar tablas > iniciar >
#  comprobar. Si la versión nueva no arranca, ofrece volver a la anterior.
#  Hazlo cuando el local esté cerrado (el sistema se detiene ~1 minuto).
# =====================================================================

param(
    [string]$Destino = 'C:\RestaurantePOS',
    [switch]$SinRespaldo
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$Servicio = $script:NombreServicio
$Origen = $PSScriptRoot
$App = Join-Path $Destino 'app'
$Herramientas = Join-Path $Destino 'herramientas'
$Config = Join-Path $App 'appsettings.Production.json'
$Opciones = Join-Path $Destino 'respaldos\mysql-respaldo.cnf'
$Versiones = Join-Path $Destino 'versiones'

try {
    Escribir-Paso 'Comprobando'
    if (-not (Es-Administrador)) { Detener 'Ejecuta como ADMINISTRADOR.' }
    if (-not (Get-Service -Name $Servicio -ErrorAction SilentlyContinue)) { Detener "No existe el servicio ${Servicio}: usa INSTALAR.bat." }
    if (-not (Test-Path $Config)) { Detener "No encuentro $Config." }
    if (-not (Test-Path (Join-Path $Origen 'app\CaraNegra.API.exe'))) { Detener 'Ejecuta este script desde la carpeta del paquete NUEVO.' }
    $actual = if (Test-Path (Join-Path $Herramientas 'version.txt')) { (Get-Content (Join-Path $Herramientas 'version.txt') -TotalCount 1) } else { '(desconocida)' }
    $nueva = if (Test-Path (Join-Path $Origen 'version.txt')) { (Get-Content (Join-Path $Origen 'version.txt') -TotalCount 1) } else { '(desconocida)' }
    Write-Host "    Versión instalada: $actual"
    Write-Host "    Versión nueva:     $nueva"
    if (-not (Preguntar-SiNo '¿Actualizar ahora? El sistema se detendrá aproximadamente 1 minuto.' $true)) { Write-Host 'Cancelado.'; exit 0 }

    $mysql = Buscar-MySql
    $conexion = Leer-Conexion $Config
    $puerto = Leer-Puerto $Config

    if (-not $SinRespaldo) {
        Escribir-Paso 'Respaldo de la base de datos antes de actualizar'
        & (Join-Path $Herramientas 'respaldo-mysql.ps1') -Destino $Destino
        Escribir-Ok 'Respaldo hecho'
    }

    Escribir-Paso 'Deteniendo el servicio'
    Stop-Service -Name $Servicio -Force
    if (-not (Esperar-Servicio $Servicio 'Stopped' 60)) {
        Get-Process -Name 'CaraNegra.API' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 3
    }
    Escribir-Ok 'Detenido'

    Escribir-Paso 'Guardando una copia de la versión actual'
    $copia = Join-Path $Versiones ("app-" + (Get-Date -Format 'yyyyMMdd-HHmm'))
    & robocopy $App $copia /MIR /XD logs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Detener "No se pudo copiar la versión actual (robocopy $LASTEXITCODE)." }
    # Se conservan solo las 3 últimas copias.
    Get-ChildItem $Versiones -Directory | Sort-Object Name -Descending | Select-Object -Skip 3 | Remove-Item -Recurse -Force
    Escribir-Ok "Copia en $copia"

    Escribir-Paso 'Copiando la versión nueva (se conserva la configuración y los logs)'
    & robocopy (Join-Path $Origen 'app') $App /MIR /XD logs /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Detener "Falló la copia (robocopy $LASTEXITCODE). Restaura con: robocopy `"$copia`" `"$App`" /MIR /XD logs" }
    foreach ($f in @('comun.ps1', 'respaldo-mysql.ps1', 'restaurar-respaldo.ps1', 'desinstalar.ps1', 'DESINSTALAR.bat', 'RESPALDAR-AHORA.bat',
                     'configurar-impresora.ps1', 'CONFIGURAR-IMPRESORA.bat', 'INSTALACION_WINDOWS.md', 'version.txt')) {
        $ruta = Join-Path $Origen $f
        if (Test-Path $ruta) { Copy-Item $ruta $Herramientas -Force }
    }
    Escribir-Ok 'Archivos actualizados'

    Escribir-Paso 'Actualizando las tablas de la base de datos'
    Ejecutar-MySql -MySql $mysql -Opciones $Opciones -Archivo (Join-Path $Origen 'sql\01-estructura.sql') -BaseDatos $conexion.BaseDatos | Out-Null
    Escribir-Ok 'Estructura al día'

    Escribir-Paso 'Iniciando el servicio'
    Start-Service -Name $Servicio
    if ((Esperar-Servicio $Servicio 'Running' 60) -and (Probar-Sistema $puerto 90)) {
        Write-Host ''
        Write-Host "ACTUALIZACIÓN TERMINADA: versión $nueva en línea." -ForegroundColor Green
        Write-Host 'En los celulares y tablets, recarguen la página (o cierren y abran la app).'
    }
    else {
        Escribir-Aviso 'La versión nueva no responde.'
        if (Preguntar-SiNo '¿Volver a la versión anterior?' $true) {
            Stop-Service -Name $Servicio -Force -ErrorAction SilentlyContinue
            Esperar-Servicio $Servicio 'Stopped' 60 | Out-Null
            & robocopy $copia $App /MIR /XD logs /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
            Start-Service -Name $Servicio
            if (Probar-Sistema $puerto 90) { Escribir-Ok 'Se volvió a la versión anterior y está en línea.' }
            else { Escribir-Aviso "Tampoco responde. Revisa $App\logs (guía, sección 9)." }
        }
    }
}
catch {
    Write-Host "La actualización se detuvo: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Si el servicio quedó detenido, inícialo con: Start-Service RestaurantePOS' -ForegroundColor Yellow
    exit 1
}
