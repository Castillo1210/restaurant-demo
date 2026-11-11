# =====================================================================
#  instalar.ps1 - Instala el Restaurante POS como SERVICIO DE WINDOWS.
#
#  Ejecutar como ADMINISTRADOR desde la carpeta del paquete (la que tiene
#  la carpeta "app"), con doble clic en INSTALAR.bat (clic derecho >
#  Ejecutar como administrador) o en PowerShell:
#      powershell -ExecutionPolicy Bypass -File .\instalar.ps1
#
#  Qué hace (ver INSTALACION_WINDOWS.md, sección 6):
#    1. Verifica MySQL y que el puerto esté libre.
#    2. Copia el programa a C:\RestaurantePOS\app.
#    3. Crea la base de datos y su usuario, la estructura (tablas) y,
#       si quieres, carga la carta.
#    4. Genera appsettings.Production.json (conexión, clave JWT, puerto).
#    5. Crea el servicio "RestaurantePOS" (inicio automático, se reinicia
#       solo si falla y espera a MySQL).
#    6. Abre el puerto en el firewall y programa el respaldo diario.
#    7. Inicia el servicio y comprueba que responda.
# =====================================================================

param(
    [string]$Destino = 'C:\RestaurantePOS',
    [int]$Puerto = 80,
    [string]$BaseDatos = 'restaurante',
    [string]$UsuarioBd = 'restaurante',
    # Usa una base de datos que YA existe (p. ej. restaurada de la demo). Se pedirá su usuario y clave.
    [switch]$BaseExistente,
    [string]$HoraRespaldo = '03:00'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$Servicio = $script:NombreServicio
$Origen = $PSScriptRoot
$App = Join-Path $Destino 'app'
$Herramientas = Join-Path $Destino 'herramientas'
$Respaldos = Join-Path $Destino 'respaldos'
$Config = Join-Path $App 'appsettings.Production.json'
$Exe = Join-Path $App 'CaraNegra.API.exe'
$OpcionesRespaldo = Join-Path $Respaldos 'mysql-respaldo.cnf'
$opcionesRoot = $null

try {
    Write-Host ''
    Write-Host '=====================================================' -ForegroundColor White
    Write-Host '   Instalación del Restaurante POS (servicio Windows)' -ForegroundColor White
    Write-Host '=====================================================' -ForegroundColor White

    # ── 0. Comprobaciones ──────────────────────────────────────────────
    Escribir-Paso 'Comprobando requisitos'
    if (-not (Es-Administrador)) { Detener 'Ejecuta este script como ADMINISTRADOR (clic derecho > Ejecutar como administrador).' }
    if (-not (Test-Path (Join-Path $Origen 'app\CaraNegra.API.exe'))) { Detener "No encuentro app\CaraNegra.API.exe junto a este script. Ejecútalo desde la carpeta del paquete descomprimido." }
    if (Get-Service -Name $Servicio -ErrorAction SilentlyContinue) {
        Detener "El servicio $Servicio ya existe. Para instalar una versión nueva usa ACTUALIZAR.bat; para reinstalar desde cero, primero ejecuta DESINSTALAR.bat."
    }

    $mysql = Buscar-MySql
    if (-not $mysql) { Detener 'No encontré MySQL Server instalado como servicio. Instálalo primero (guía, sección 3).' }
    if (-not (Test-Path $mysql.Mysql)) { Detener "No encontré mysql.exe en $($mysql.Bin)." }
    Set-Service -Name $mysql.Servicio -StartupType Automatic
    if ($mysql.Estado -ne 'Running') {
        Start-Service -Name $mysql.Servicio
        if (-not (Esperar-Servicio $mysql.Servicio 'Running' 60)) { Detener "El servicio $($mysql.Servicio) de MySQL no inicia." }
    }
    Escribir-Ok "MySQL encontrado: servicio $($mysql.Servicio) ($($mysql.Bin))"

    $ocupado = Get-NetTCPConnection -LocalPort $Puerto -State Listen -ErrorAction SilentlyContinue
    if ($ocupado) {
        $proceso = (Get-Process -Id ($ocupado | Select-Object -First 1).OwningProcess -ErrorAction SilentlyContinue).ProcessName
        Detener "El puerto $Puerto ya está en uso (programa: $proceso). Cierra ese programa o instala en otro puerto: .\instalar.ps1 -Puerto 8080"
    }
    Escribir-Ok "Puerto $Puerto libre"

    # ── 1. Copiar el programa ─────────────────────────────────────────
    Escribir-Paso "Copiando el programa a $App"
    foreach ($c in @($Destino, $App, $Herramientas, $Respaldos)) {
        if (-not (Test-Path $c)) { New-Item -ItemType Directory -Path $c -Force | Out-Null }
    }
    & robocopy (Join-Path $Origen 'app') $App /MIR /XD logs /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Detener "Falló la copia de archivos (robocopy $LASTEXITCODE)." }
    foreach ($f in @('comun.ps1', 'respaldo-mysql.ps1', 'restaurar-respaldo.ps1', 'desinstalar.ps1', 'DESINSTALAR.bat', 'RESPALDAR-AHORA.bat',
                     'configurar-impresora.ps1', 'CONFIGURAR-IMPRESORA.bat', 'INSTALACION_WINDOWS.md', 'version.txt')) {
        $ruta = Join-Path $Origen $f
        if (Test-Path $ruta) { Copy-Item $ruta $Herramientas -Force }
    }
    New-Item -ItemType Directory -Path (Join-Path $App 'logs') -Force | Out-Null
    Escribir-Ok 'Programa copiado'

    # ── 2. Base de datos ──────────────────────────────────────────────
    $claveBd = $null
    $baseNueva = $false
    if ($BaseExistente) {
        Escribir-Paso "Usando la base de datos existente '$BaseDatos'"
        $respuesta = (Read-Host "    Usuario de MySQL de esa base [$UsuarioBd]").Trim()
        if ($respuesta) { $UsuarioBd = $respuesta }
        $claveBd = Texto-Plano (Read-Host "    Contraseña de ese usuario" -AsSecureString)
        Escribir-ArchivoOpciones $OpcionesRespaldo $UsuarioBd $claveBd
        Ejecutar-MySql -MySql $mysql -Opciones $OpcionesRespaldo -Sql 'SELECT 1;' -BaseDatos $BaseDatos | Out-Null
        Escribir-Ok 'Conexión correcta'
    }
    else {
        Escribir-Paso "Creando la base de datos '$BaseDatos' y su usuario '$UsuarioBd'"
        Write-Host '    Se necesita la contraseña de "root" de MySQL (la que pusiste al instalar MySQL).'
        $claveRoot = Texto-Plano (Read-Host '    Contraseña de root' -AsSecureString)
        $opcionesRoot = Join-Path $env:TEMP ('rpos-root-' + [guid]::NewGuid().ToString('N') + '.cnf')
        Escribir-ArchivoOpciones $opcionesRoot 'root' $claveRoot
        $claveRoot = $null
        try {
            Ejecutar-MySql -MySql $mysql -Opciones $opcionesRoot -Sql 'SELECT 1;' | Out-Null
        }
        catch { Detener "No se pudo entrar a MySQL como root. ¿La contraseña es correcta? ($($_.Exception.Message))" }

        $existe = Ejecutar-MySql -MySql $mysql -Opciones $opcionesRoot -SinEncabezados `
            -Sql "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$BaseDatos';"
        $tablas = [int](($existe | Out-String).Trim())
        if ($tablas -gt 0) {
            Escribir-Aviso "La base '$BaseDatos' ya existe y tiene $tablas tablas."
            if (-not (Preguntar-SiNo '¿Usarla tal cual (se conservan sus datos)?' $true)) {
                Detener "Instalación cancelada. Usa otro nombre: .\instalar.ps1 -BaseDatos restaurante2"
            }
        }
        else { $baseNueva = $true }

        $claveBd = Nueva-Clave 24
        $sqlUsuario = @"
CREATE DATABASE IF NOT EXISTS ``$BaseDatos`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$UsuarioBd'@'localhost' IDENTIFIED BY '$claveBd';
CREATE USER IF NOT EXISTS '$UsuarioBd'@'127.0.0.1' IDENTIFIED BY '$claveBd';
ALTER USER '$UsuarioBd'@'localhost' IDENTIFIED BY '$claveBd';
ALTER USER '$UsuarioBd'@'127.0.0.1' IDENTIFIED BY '$claveBd';
GRANT ALL PRIVILEGES ON ``$BaseDatos``.* TO '$UsuarioBd'@'localhost';
GRANT ALL PRIVILEGES ON ``$BaseDatos``.* TO '$UsuarioBd'@'127.0.0.1';
FLUSH PRIVILEGES;
"@
        Ejecutar-MySql -MySql $mysql -Opciones $opcionesRoot -Sql $sqlUsuario | Out-Null
        Remove-Item $opcionesRoot -Force -ErrorAction SilentlyContinue
        $opcionesRoot = $null
        Escribir-ArchivoOpciones $OpcionesRespaldo $UsuarioBd $claveBd
        Escribir-Ok "Base '$BaseDatos' y usuario '$UsuarioBd' listos (la contraseña se generó sola y se guarda en la configuración)"
    }

    Escribir-Paso 'Creando / actualizando las tablas (estructura)'
    # Base nueva: script completo. Base existente: script idempotente (solo aplica lo que falte).
    $estructura = if ($baseNueva) { Join-Path $Origen 'sql\01-estructura-base-nueva.sql' } else { Join-Path $Origen 'sql\01-estructura.sql' }
    if (-not (Test-Path $estructura)) { Detener "Falta $estructura en el paquete. Vuelve a generar el paquete con publicar.ps1." }
    Ejecutar-MySql -MySql $mysql -Opciones $OpcionesRespaldo -Archivo $estructura -BaseDatos $BaseDatos | Out-Null
    Escribir-Ok 'Estructura al día'

    $carta = Join-Path $Origen 'sql\02-carta-la-casa-del-cuy.sql'
    if ((Test-Path $carta) -and $baseNueva) {
        if (Preguntar-SiNo '¿Cargar la carta de La Casa del Cuy (categorías y productos)?' $true) {
            Ejecutar-MySql -MySql $mysql -Opciones $OpcionesRespaldo -Archivo $carta -BaseDatos $BaseDatos | Out-Null
            Escribir-Ok 'Carta cargada'
        }
    }

    # ── 3. Configuración ──────────────────────────────────────────────
    Escribir-Paso 'Generando la configuración (appsettings.Production.json)'
    if (Test-Path $Config) {
        $cfg = Get-Content $Config -Raw -Encoding UTF8 | ConvertFrom-Json
        Escribir-Aviso 'Ya existía una configuración: se conserva y solo se actualizan la conexión y el puerto.'
    }
    else {
        $cfg = Get-Content (Join-Path $Origen 'appsettings.Production.ejemplo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $cfg.JwtSettings.Secret = Nueva-Clave 64
    }
    $cfg.ConnectionStrings.DefaultConnection = "Server=127.0.0.1;Port=3306;Database=$BaseDatos;User=$UsuarioBd;Password=$claveBd;"
    $cfg.Kestrel.Endpoints.Http.Url = "http://0.0.0.0:$Puerto"

    Write-Host '    (Puedes dejar en blanco y completarlo después editando el archivo.)'
    $tokenDecolecta = (Read-Host '    Token de Decolecta para consultar DNI/RUC').Trim()
    if ($tokenDecolecta) { $cfg.ConsultaDocumentos.Token = $tokenDecolecta }
    if (Preguntar-SiNo '¿Emitir comprobantes REALES con APISUNAT desde ya? (No = modo simulado con leyenda DEMO)' $false) {
        $tokenApiSunat = (Read-Host '    Token de PRODUCCIÓN de APISUNAT').Trim()
        if ($tokenApiSunat) {
            $cfg.FacturacionElectronica.Proveedor = 'ApiSunat'
            $cfg.FacturacionElectronica.Ambiente = 'Produccion'
            $cfg.FacturacionElectronica.Token = $tokenApiSunat
        }
        else { Escribir-Aviso 'Sin token: se queda en modo Simulado.' }
    }

    # Ticketera de COCINA (comandas, por red). La de CAJA es la impresora predeterminada de Windows.
    Configurar-ImpresoraCocina $cfg

    Guardar-Texto $Config ($cfg | ConvertTo-Json -Depth 10)
    Escribir-Ok "Configuración guardada en $Config"

    # ── 4. Servicio de Windows ────────────────────────────────────────
    Escribir-Paso "Creando el servicio de Windows '$Servicio'"
    New-Service -Name $Servicio -BinaryPathName "`"$Exe`"" -DisplayName 'Restaurante POS' `
        -Description 'Sistema del restaurante (pedidos, cocina, caja y comprobantes). Atiende en el puerto configurado.' `
        -StartupType Automatic | Out-Null
    # Inicio automático (retrasado): arranca solo al encender la PC, después de la red y de MySQL.
    & sc.exe config $Servicio start= delayed-auto | Out-Null
    # Cuenta virtual propia (más segura que LocalSystem): NT SERVICE\RestaurantePOS
    & sc.exe config $Servicio obj= "NT SERVICE\$Servicio" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Escribir-Aviso 'No se pudo asignar la cuenta virtual; el servicio usará la cuenta del sistema (LocalSystem).'
    }
    # Depende de MySQL: Windows inicia primero MySQL.
    & sc.exe config $Servicio depend= $mysql.Servicio | Out-Null
    # Si se cae, Windows lo vuelve a iniciar (a los 10 s, 30 s y 60 s).
    & sc.exe failure $Servicio reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
    & sc.exe failureflag $Servicio 1 | Out-Null
    Escribir-Ok 'Servicio creado (inicio automático, reinicio si falla, depende de MySQL)'

    # ── 5. Permisos de carpetas ───────────────────────────────────────
    Escribir-Paso 'Ajustando permisos'
    $cuenta = "NT SERVICE\$Servicio"
    & icacls $Destino /grant "${cuenta}:(OI)(CI)RX" /Q | Out-Null
    & icacls (Join-Path $App 'logs') /grant "${cuenta}:(OI)(CI)M" /Q | Out-Null
    # La configuración (con claves) solo la leen administradores, el sistema y el servicio.
    & icacls $Config /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' "${cuenta}:R" /Q | Out-Null
    # Los respaldos solo los ven administradores y el sistema.
    & icacls $Respaldos /inheritance:r /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' /Q | Out-Null
    Escribir-Ok 'Permisos aplicados'

    # ── 6. Firewall ───────────────────────────────────────────────────
    Escribir-Paso "Abriendo el puerto $Puerto en el firewall (red privada)"
    Get-NetFirewallRule -Name 'RestaurantePOS-HTTP' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    $perfiles = 'Private,Domain'
    $publicas = Get-NetConnectionProfile -ErrorAction SilentlyContinue | Where-Object { $_.NetworkCategory -eq 'Public' }
    if ($publicas) {
        Escribir-Aviso "La red '$(($publicas | Select-Object -First 1).Name)' está marcada como PÚBLICA: así los celulares no podrían conectarse."
        if (Preguntar-SiNo '¿Marcarla como red PRIVADA (recomendado en la red del local)?' $true) {
            $publicas | Set-NetConnectionProfile -NetworkCategory Private
            Escribir-Ok 'Red marcada como privada'
        }
        else {
            $perfiles = 'Any'
            Escribir-Aviso 'Se abrirá el puerto también en redes públicas.'
        }
    }
    New-NetFirewallRule -Name 'RestaurantePOS-HTTP' -DisplayName "Restaurante POS (puerto $Puerto)" -Direction Inbound `
        -Protocol TCP -LocalPort $Puerto -Action Allow -Profile $perfiles | Out-Null
    Escribir-Ok 'Regla de firewall creada'

    # ── 7. Respaldo diario ────────────────────────────────────────────
    Escribir-Paso "Programando el respaldo diario de la base de datos ($HoraRespaldo)"
    $tarea = 'RestaurantePOS - Respaldo diario'
    Unregister-ScheduledTask -TaskName $tarea -Confirm:$false -ErrorAction SilentlyContinue
    $accion = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$Herramientas\respaldo-mysql.ps1`""
    $disparador = New-ScheduledTaskTrigger -Daily -At $HoraRespaldo
    # Si a esa hora la PC estaba apagada, se hace apenas se encienda.
    $ajustes = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $tarea -Action $accion -Trigger $disparador -Settings $ajustes -Principal $principal | Out-Null
    Escribir-Ok "Tarea '$tarea' creada (los respaldos quedan en $Respaldos)"

    # ── 8. Iniciar y comprobar ────────────────────────────────────────
    Escribir-Paso 'Iniciando el servicio'
    Start-Service -Name $Servicio
    if (-not (Esperar-Servicio $Servicio 'Running' 60)) { Detener "El servicio no quedó en ejecución. Revisa $App\logs y el Visor de eventos (guía, sección 9)." }
    if (Probar-Sistema $Puerto 90) { Escribir-Ok "El sistema responde en http://localhost:$Puerto" }
    else { Escribir-Aviso "El servicio está en ejecución pero aún no responde en http://localhost:$Puerto/health. Revisa $App\logs." }

    Escribir-Paso 'Primer respaldo de prueba'
    try {
        & (Join-Path $Herramientas 'respaldo-mysql.ps1')
        Escribir-Ok 'Respaldo de prueba correcto'
    }
    catch { Escribir-Aviso "El respaldo de prueba falló: $($_.Exception.Message)" }

    # ── Resumen ──────────────────────────────────────────────────────
    Write-Host ''
    Write-Host '=====================================================' -ForegroundColor Green
    Write-Host '   INSTALACIÓN TERMINADA' -ForegroundColor Green
    Write-Host '=====================================================' -ForegroundColor Green
    $sufijo = if ($Puerto -eq 80) { '' } else { ":$Puerto" }
    Write-Host "  En esta PC:          http://localhost$sufijo"
    foreach ($ip in (IPs-Locales)) { Write-Host "  Celulares / tablets: http://$ip$sufijo" -ForegroundColor White }

    Start-Sleep -Seconds 2
    $clave = Select-String -Path (Join-Path $App 'logs\*.log') -Pattern 'Contraseña temporal: (\S+)' -ErrorAction SilentlyContinue |
        Select-Object -Last 1
    if ($clave) {
        Write-Host ''
        Write-Host '  Usuario ADMIN inicial:  admin' -ForegroundColor Yellow
        Write-Host "  Contraseña temporal:    $($clave.Matches[0].Groups[1].Value)" -ForegroundColor Yellow
        Write-Host '  -> Anótala, entra y cámbiala desde tu perfil.' -ForegroundColor Yellow
    }
    Write-Host ''
    Write-Host "  Programa:      $App"
    Write-Host "  Configuración: $Config"
    Write-Host "  Respaldos:     $Respaldos"
    Write-Host "  Ticketera de cocina: cámbiala cuando quieras con $Herramientas\CONFIGURAR-IMPRESORA.bat"
    Write-Host ''
}
catch {
    Write-Host ''
    Write-Host "La instalación se detuvo: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    if ($opcionesRoot -and (Test-Path $opcionesRoot)) { Remove-Item $opcionesRoot -Force -ErrorAction SilentlyContinue }
}
