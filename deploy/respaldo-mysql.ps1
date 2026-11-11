# =====================================================================
#  respaldo-mysql.ps1 - Respaldo de la base de datos del Restaurante POS.
#
#  Lo ejecuta la tarea programada "RestaurantePOS - Respaldo diario" y
#  también se puede ejecutar a mano (RESPALDAR-AHORA.bat).
#  Deja un .zip por día en C:\RestaurantePOS\respaldos y borra los que
#  tengan más de -DiasConservar días.
#
#  Copia extra (MUY recomendado): crea el archivo
#     C:\RestaurantePOS\respaldos\copia-extra.txt
#  con la ruta de otra carpeta en la primera línea (una USB, un disco
#  externo o una carpeta sincronizada con Google Drive / OneDrive), y
#  cada respaldo se copiará también allí.
# =====================================================================

param(
    [string]$Destino = (Split-Path -Parent $PSScriptRoot),
    [int]$DiasConservar = 30
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$Respaldos = Join-Path $Destino 'respaldos'
$Config = Join-Path $Destino 'app\appsettings.Production.json'
$Opciones = Join-Path $Respaldos 'mysql-respaldo.cnf'
$Bitacora = Join-Path $Respaldos 'respaldo.log'

function Anotar([string]$Texto) {
    $linea = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $Texto"
    Add-Content -Path $Bitacora -Value $linea -Encoding UTF8
    Write-Host "    $Texto"
}

try {
    if (-not (Test-Path $Opciones)) { throw "Falta $Opciones (lo crea instalar.ps1)." }
    $conexion = Leer-Conexion $Config
    $mysql = Buscar-MySql
    if (-not $mysql -or -not (Test-Path $mysql.Mysqldump)) { throw 'No encontré mysqldump.exe (¿MySQL está instalado?).' }

    # Si la PC recién se encendió, MySQL puede estar arrancando: se espera hasta 3 minutos.
    $limite = (Get-Date).AddMinutes(3)
    while ((Get-Service $mysql.Servicio).Status -ne 'Running' -and (Get-Date) -lt $limite) { Start-Sleep -Seconds 10 }

    $marca = Get-Date -Format 'yyyy-MM-dd_HHmm'
    $sql = Join-Path $Respaldos "$($conexion.BaseDatos)_$marca.sql"
    $zip = "$sql.zip"
    $errores = Join-Path $env:TEMP ('rpos-dump-' + [guid]::NewGuid().ToString('N') + '.err')

    $argumentos = @(
        "`"--defaults-extra-file=$Opciones`"",
        '--single-transaction', '--no-tablespaces',
        '--default-character-set=utf8mb4', '--add-drop-table',
        # Fotos de los productos (columnas binarias) en hexadecimal: el respaldo queda en texto puro
        # y se restaura sin que la conversión de caracteres dañe ninguna imagen.
        '--hex-blob',
        "`"--result-file=$sql`"",
        $conexion.BaseDatos
    )
    $p = Start-Process -FilePath $mysql.Mysqldump -ArgumentList $argumentos -NoNewWindow -Wait -PassThru -RedirectStandardError $errores
    $textoError = Get-Content $errores -Raw -ErrorAction SilentlyContinue
    Remove-Item $errores -Force -ErrorAction SilentlyContinue
    if ($p.ExitCode -ne 0 -or -not (Test-Path $sql) -or (Get-Item $sql).Length -lt 1000) {
        throw "mysqldump falló ($($p.ExitCode)): $textoError"
    }

    Compress-Archive -Path $sql -DestinationPath $zip -Force
    Remove-Item $sql -Force
    $tamano = [math]::Round((Get-Item $zip).Length / 1KB)
    Anotar "OK  $([IO.Path]::GetFileName($zip)) ($tamano KB)"

    # Copia extra (USB / disco externo / carpeta sincronizada con la nube)
    $archivoExtra = Join-Path $Respaldos 'copia-extra.txt'
    if (Test-Path $archivoExtra) {
        $extra = (Get-Content $archivoExtra -TotalCount 1).Trim()
        if ($extra -and (Test-Path $extra)) {
            Copy-Item $zip $extra -Force
            Anotar "    copiado también a $extra"
        }
        elseif ($extra) { Anotar "!!  no se encontró la carpeta de copia extra: $extra" }
    }

    # Limpieza de respaldos viejos
    Get-ChildItem $Respaldos -Filter '*.sql.zip' |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$DiasConservar) } |
        Remove-Item -Force
}
catch {
    Anotar "ERROR  $($_.Exception.Message)"
    throw
}
