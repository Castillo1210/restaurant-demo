# =====================================================================
#  restaurar-respaldo.ps1 - Vuelve la base de datos al estado de un respaldo.
#
#  ¡CUIDADO! Reemplaza TODOS los datos actuales por los del respaldo.
#  Uso (PowerShell como administrador, en C:\RestaurantePOS\herramientas):
#     powershell -ExecutionPolicy Bypass -File .\restaurar-respaldo.ps1 -Archivo "C:\RestaurantePOS\respaldos\restaurante_2026-10-04_0300.sql.zip"
# =====================================================================

param(
    [Parameter(Mandatory)] [string]$Archivo,
    [string]$Destino = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

try {
    if (-not (Es-Administrador)) { Detener 'Ejecuta como ADMINISTRADOR.' }
    if (-not (Test-Path $Archivo)) { Detener "No existe $Archivo" }

    $Config = Join-Path $Destino 'app\appsettings.Production.json'
    $Opciones = Join-Path $Destino 'respaldos\mysql-respaldo.cnf'
    $conexion = Leer-Conexion $Config
    $mysql = Buscar-MySql

    Write-Host ''
    Write-Host "Se va a REEMPLAZAR la base '$($conexion.BaseDatos)' con el respaldo:" -ForegroundColor Yellow
    Write-Host "   $Archivo" -ForegroundColor Yellow
    if (-not (Preguntar-SiNo '¿Continuar?' $false)) { Write-Host 'Cancelado.'; exit 0 }

    Escribir-Paso 'Respaldo de seguridad del estado actual (por si acaso)'
    & (Join-Path $PSScriptRoot 'respaldo-mysql.ps1') -Destino $Destino

    Escribir-Paso 'Deteniendo el servicio'
    Stop-Service -Name $script:NombreServicio -ErrorAction SilentlyContinue
    Esperar-Servicio $script:NombreServicio 'Stopped' 60 | Out-Null

    $temporal = Join-Path $env:TEMP ('rpos-restaurar-' + [guid]::NewGuid().ToString('N'))
    try {
        if ($Archivo -like '*.zip') {
            Expand-Archive -Path $Archivo -DestinationPath $temporal -Force
            $sql = Get-ChildItem $temporal -Filter '*.sql' | Select-Object -First 1 -ExpandProperty FullName
        }
        else { $sql = $Archivo }
        if (-not $sql) { Detener 'El respaldo no contiene un archivo .sql' }

        Escribir-Paso 'Restaurando (puede tardar unos minutos)'
        Ejecutar-MySql -MySql $mysql -Opciones $Opciones -Archivo $sql -BaseDatos $conexion.BaseDatos | Out-Null
        Escribir-Ok 'Datos restaurados'
    }
    finally {
        if (Test-Path $temporal) { Remove-Item $temporal -Recurse -Force -ErrorAction SilentlyContinue }
    }

    Escribir-Paso 'Iniciando el servicio'
    Start-Service -Name $script:NombreServicio
    if (Probar-Sistema (Leer-Puerto $Config) 90) { Escribir-Ok 'Sistema en línea' }
    else { Escribir-Aviso 'El servicio inició pero aún no responde; revisa los logs.' }
}
catch {
    Write-Host "La restauración se detuvo: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
