# =====================================================================
#  comun.ps1 - Funciones compartidas por los scripts de instalacion.
#  No se ejecuta solo: lo cargan instalar.ps1, actualizar.ps1, etc.
#  Compatible con Windows PowerShell 5.1 (el que trae Windows 10/11).
# =====================================================================

$script:NombreServicio = 'RestaurantePOS'

function Escribir-Paso([string]$Texto) {
    Write-Host ''
    Write-Host "==> $Texto" -ForegroundColor Cyan
}

function Escribir-Ok([string]$Texto) {
    Write-Host "    OK  $Texto" -ForegroundColor Green
}

function Escribir-Aviso([string]$Texto) {
    Write-Host "    !!  $Texto" -ForegroundColor Yellow
}

function Detener([string]$Texto) {
    Write-Host ''
    Write-Host "ERROR: $Texto" -ForegroundColor Red
    Write-Host ''
    throw $Texto
}

function Es-Administrador {
    $identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identidad)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Guarda texto en UTF-8 SIN BOM (MySQL no acepta el BOM en sus archivos de opciones).
function Guardar-Texto([string]$Ruta, [string]$Texto) {
    $carpeta = Split-Path -Parent $Ruta
    if ($carpeta -and -not (Test-Path $carpeta)) { New-Item -ItemType Directory -Path $carpeta -Force | Out-Null }
    [IO.File]::WriteAllText($Ruta, $Texto, (New-Object Text.UTF8Encoding($false)))
}

# Clave aleatoria solo con letras y numeros (sin simbolos: no hay problemas de comillas en
# cadenas de conexion, SQL ni archivos de opciones).
function Nueva-Clave([int]$Largo = 32) {
    $caracteres = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-Object byte[] $Largo
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    return -join ($bytes | ForEach-Object { $caracteres[$_ % $caracteres.Length] })
}

function Texto-Plano([Security.SecureString]$Seguro) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Seguro)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Preguntar-SiNo([string]$Pregunta, [bool]$PorDefecto = $true) {
    $opciones = if ($PorDefecto) { '[S/n]' } else { '[s/N]' }
    while ($true) {
        $r = (Read-Host "    $Pregunta $opciones").Trim().ToLower()
        if ($r -eq '') { return $PorDefecto }
        if ($r -in @('s', 'si', 'sí', 'y', 'yes')) { return $true }
        if ($r -in @('n', 'no')) { return $false }
    }
}

# Busca el servicio de MySQL instalado y la carpeta bin (mysql.exe, mysqldump.exe).
function Buscar-MySql {
    $servicio = Get-CimInstance Win32_Service |
        Where-Object { $_.PathName -match 'mysqld' } |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if (-not $servicio) { return $null }

    $exe = $null
    if ($servicio.PathName -match '"([^"]*mysqld(\.exe)?)"') { $exe = $Matches[1] }
    elseif ($servicio.PathName -match '^(\S*mysqld(\.exe)?)') { $exe = $Matches[1] }
    if (-not $exe) { return $null }

    $bin = Split-Path -Parent $exe
    return [pscustomobject]@{
        Servicio  = $servicio.Name
        Estado    = $servicio.State
        Bin       = $bin
        Mysql     = Join-Path $bin 'mysql.exe'
        Mysqldump = Join-Path $bin 'mysqldump.exe'
    }
}

# Archivo de opciones de MySQL ([client]) para no escribir la clave en la linea de comandos.
function Escribir-ArchivoOpciones([string]$Ruta, [string]$Usuario, [string]$Clave) {
    $c = $Clave.Replace('\', '\\').Replace('"', '\"')
    $texto = "[client]`r`nuser=$Usuario`r`npassword=`"$c`"`r`nhost=localhost`r`nport=3306`r`ndefault-character-set=utf8mb4`r`n"
    Guardar-Texto $Ruta $texto
}

# Ejecuta mysql.exe con un archivo .sql (o un texto SQL). Devuelve la salida estandar.
function Ejecutar-MySql {
    param(
        [Parameter(Mandatory)] $MySql,
        [Parameter(Mandatory)] [string]$Opciones,
        [string]$Archivo,
        [string]$Sql,
        [string]$BaseDatos,
        [switch]$SinEncabezados
    )
    $temporal = $null
    if ($Sql) {
        $temporal = Join-Path $env:TEMP ("rpos-" + [guid]::NewGuid().ToString('N') + '.sql')
        Guardar-Texto $temporal $Sql
        $Archivo = $temporal
    }
    $salida = Join-Path $env:TEMP ("rpos-" + [guid]::NewGuid().ToString('N') + '.out')
    $errores = Join-Path $env:TEMP ("rpos-" + [guid]::NewGuid().ToString('N') + '.err')
    try {
        $argumentos = @("`"--defaults-extra-file=$Opciones`"")
        if ($SinEncabezados) { $argumentos += '--skip-column-names'; $argumentos += '--batch' }
        if ($BaseDatos) { $argumentos += "--database=$BaseDatos" }
        $p = Start-Process -FilePath $MySql.Mysql -ArgumentList $argumentos -NoNewWindow -Wait -PassThru `
            -RedirectStandardInput $Archivo -RedirectStandardOutput $salida -RedirectStandardError $errores
        $textoError = (Get-Content $errores -Raw -ErrorAction SilentlyContinue)
        if ($p.ExitCode -ne 0) {
            throw "mysql.exe termino con error ($($p.ExitCode)): $textoError"
        }
        return (Get-Content $salida -Raw -ErrorAction SilentlyContinue)
    }
    finally {
        foreach ($f in @($temporal, $salida, $errores)) {
            if ($f -and (Test-Path $f)) { Remove-Item $f -Force -ErrorAction SilentlyContinue }
        }
    }
}

# Lee un valor de la cadena de conexion ("Database", "User", ...) de appsettings.Production.json.
function Leer-Conexion([string]$RutaConfig) {
    $cfg = Get-Content $RutaConfig -Raw -Encoding UTF8 | ConvertFrom-Json
    $cadena = $cfg.ConnectionStrings.DefaultConnection
    $valores = @{}
    foreach ($parte in $cadena.Split(';')) {
        $kv = $parte.Split('=', 2)
        if ($kv.Count -eq 2) { $valores[$kv[0].Trim().ToLower()] = $kv[1].Trim() }
    }
    return [pscustomobject]@{
        BaseDatos = $valores['database']
        Usuario   = if ($valores['user']) { $valores['user'] } else { $valores['user id'] }
        Clave     = $valores['password']
        Puerto    = $valores['port']
    }
}

function Esperar-Servicio([string]$Nombre, [string]$Estado, [int]$Segundos = 60) {
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        $s = Get-Service -Name $Nombre -ErrorAction SilentlyContinue
        if ($s -and $s.Status -eq $Estado) { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

# Espera a que el sistema responda en http://localhost:PUERTO/health (la base de datos incluida).
function Probar-Sistema([int]$Puerto, [int]$Segundos = 90) {
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:$Puerto/health" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) { return $true }
        }
        catch { }
        Start-Sleep -Seconds 3
    }
    return $false
}

function Leer-Puerto([string]$RutaConfig) {
    $cfg = Get-Content $RutaConfig -Raw -Encoding UTF8 | ConvertFrom-Json
    $url = $cfg.Kestrel.Endpoints.Http.Url
    if ($url -match ':(\d+)\s*$') { return [int]$Matches[1] }
    return 80
}

# IPs de la red local de esta PC (para abrir el sistema desde los celulares).
function IPs-Locales {
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.InterfaceAlias -notmatch 'vEthernet|Loopback|VirtualBox|VMware|WSL' } |
        Select-Object -ExpandProperty IPAddress
}

# ── Ticketera de cocina (ESC/POS por red, puerto 9100) ───────────────

# ¿Responde la ticketera en IP:puerto? (solo abre y cierra la conexión, no imprime nada)
function Probar-Impresora([string]$Ip, [int]$Puerto = 9100, [int]$TimeoutMs = 3000) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try { return ($cliente.ConnectAsync($Ip, $Puerto).Wait($TimeoutMs) -and $cliente.Connected) }
    catch { return $false }
    finally { $cliente.Close() }
}

# Imprime una hoja corta de prueba (ESC/POS: texto grande, avance y corte de papel).
function Imprimir-PaginaPrueba([string]$Ip, [int]$Puerto = 9100) {
    $esc = [char]27; $gs = [char]29
    $texto = "$esc@" +                                     # inicializar
        "${esc}a" + [char]1 +                              # centrado
        "${esc}E" + [char]1 + "${gs}!" + [char]0x11 + "PRUEBA`n" +
        "${gs}!" + [char]0 + "TICKETERA DE COCINA`n" + "${esc}E" + [char]0 +
        "--------------------------------`n" +
        "Si lees esto, las comandas`nsaldran en esta impresora.`n`n" +
        "IP ${Ip}:$Puerto`n" + (Get-Date -Format 'dd/MM/yyyy HH:mm') + "  PC $env:COMPUTERNAME`n" +
        "${esc}a" + [char]0 +
        "${esc}d" + [char]4 +                              # avanzar 4 líneas
        "${gs}V" + [char]66 + [char]0                      # cortar el papel
    $bytes = [Text.Encoding]::ASCII.GetBytes($texto)

    $cliente = New-Object System.Net.Sockets.TcpClient
    try {
        if (-not $cliente.ConnectAsync($Ip, $Puerto).Wait(3000)) { throw "La ticketera no respondió en ${Ip}:$Puerto" }
        $flujo = $cliente.GetStream()
        $flujo.Write($bytes, 0, $bytes.Length)
        $flujo.Flush()
        Start-Sleep -Milliseconds 300
    }
    finally { $cliente.Close() }
}

# Pregunta la IP de la ticketera de cocina, la prueba (conexión + hoja de prueba opcional) y la
# deja en $Cfg.ImpresoraCocina. Lo usan INSTALAR.bat y CONFIGURAR-IMPRESORA.bat.
function Configurar-ImpresoraCocina($Cfg) {
    if (-not $Cfg.PSObject.Properties['ImpresoraCocina']) {
        $Cfg | Add-Member -NotePropertyName 'ImpresoraCocina' -NotePropertyValue ([pscustomobject]@{ Habilitada = $false; Ip = ''; Puerto = 9100 })
    }
    $imp = $Cfg.ImpresoraCocina
    foreach ($par in @(@('Habilitada', $false), @('Ip', ''), @('Puerto', 9100))) {
        if (-not $imp.PSObject.Properties[$par[0]]) { $imp | Add-Member -NotePropertyName $par[0] -NotePropertyValue $par[1] }
    }

    $actual = if ($imp.Habilitada -and $imp.Ip) { "$($imp.Ip):$($imp.Puerto)" } else { 'sin configurar' }
    Write-Host "    Ticketera de cocina actual: $actual"
    if (-not (Preguntar-SiNo '¿Hay una ticketera de COCINA conectada por red (cable o Wi-Fi) para las comandas?' $true)) {
        $imp.Habilitada = $false
        Escribir-Aviso 'Sin ticketera de cocina: los pedidos se verán solo en la pantalla de cocina.'
        return
    }

    $misIps = @()
    try { $misIps = @(IPs-Locales) } catch { }
    while ($true) {
        $sugerida = if ($imp.Ip) { " [Enter = $($imp.Ip)]" } else { '' }
        $ip = (Read-Host "    IP fija de la ticketera (sale en su hoja de autoprueba)$sugerida").Trim()
        if (-not $ip -and $imp.Ip) { $ip = [string]$imp.Ip }
        $direccion = $null
        if ($ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$' -or -not [System.Net.IPAddress]::TryParse($ip, [ref]$direccion)) {
            Escribir-Aviso 'Escribe una IP como 192.168.1.60'
            continue
        }

        $textoPuerto = (Read-Host '    Puerto [Enter = 9100]').Trim()
        $puerto = 9100
        if ($textoPuerto) {
            if ($textoPuerto -notmatch '^\d{1,5}$' -or [int]$textoPuerto -lt 1 -or [int]$textoPuerto -gt 65535) {
                Escribir-Aviso 'Puerto no válido; se usa 9100.'
            }
            else { $puerto = [int]$textoPuerto }
        }

        # La ticketera tiene que estar en la misma red que esta PC (mismos 3 primeros números).
        $red = ($ip -split '\.')[0..2] -join '.'
        if ($misIps.Count -gt 0 -and -not ($misIps | Where-Object { (($_ -split '\.')[0..2] -join '.') -eq $red })) {
            Escribir-Aviso "La IP $ip no parece estar en la misma red que esta PC ($($misIps -join ', ')). Revisa que sea la correcta."
        }

        Write-Host "    Probando conexión con ${ip}:$puerto ..."
        if (Probar-Impresora $ip $puerto) {
            Escribir-Ok 'La ticketera responde'
            if (Preguntar-SiNo '¿Imprimir una hoja de prueba?' $true) {
                try {
                    Imprimir-PaginaPrueba $ip $puerto
                    if (-not (Preguntar-SiNo '¿Salió la hoja de prueba en la ticketera de cocina?' $true)) {
                        Escribir-Aviso 'Si respondió pero no imprimió: revisa el papel y la tapa, y que sea una ticketera ESC/POS (80 mm) con el modo RAW / puerto 9100 activo.'
                    }
                }
                catch { Escribir-Aviso "No se pudo imprimir la prueba: $($_.Exception.Message)" }
            }
            break
        }

        Escribir-Aviso "La ticketera no respondió en ${ip}:$puerto."
        Write-Host '      - ¿Está encendida, con el cable de red conectado (o en el Wi-Fi del local)?'
        Write-Host '      - ¿La IP es la de su hoja de autoprueba? (se imprime manteniendo FEED al encenderla)'
        Write-Host '      - ¿Está en la misma red que esta PC?'
        if (Preguntar-SiNo '¿Escribir otra IP?' $true) { continue }
        if (-not (Preguntar-SiNo '¿Guardar esta IP de todos modos? (podrás probar después con CONFIGURAR-IMPRESORA.bat)' $false)) {
            Escribir-Aviso 'Ticketera de cocina sin configurar. Puedes hacerlo después con CONFIGURAR-IMPRESORA.bat.'
            $imp.Habilitada = $false
            return
        }
        break
    }

    $imp.Habilitada = $true
    $imp.Ip = $ip
    $imp.Puerto = $puerto
    Escribir-Ok "Ticketera de cocina: ${ip}:$puerto"
    Elegir-TamanoComanda $imp
}

# Tamaño de letra de los platos en la comanda de cocina (el número de mesa siempre va grande).
function Elegir-TamanoComanda($Imp) {
    $nombres = @{ 'Grande' = 'grande'; 'Intermedia' = 'intermedia'; 'Alto' = 'mediana'; 'Normal' = 'pequeña' }
    $actual = if ($Imp.PSObject.Properties['TamanoProductos'] -and $nombres.ContainsKey([string]$Imp.TamanoProductos)) { [string]$Imp.TamanoProductos } else { 'Alto' }
    Write-Host '    Letra de los platos en la comanda:'
    Write-Host '      1 = Grande     (doble alto y doble ancho; 21 letras por línea)'
    Write-Host '      2 = Intermedia (entre grande y mediana; 28 letras por línea)'
    Write-Host '      3 = Mediana    (doble alto; 42 letras por línea) - recomendada'
    Write-Host '      4 = Pequeña    (letra normal en negrita)'
    $r = (Read-Host "    Elige 1, 2, 3 o 4 [Enter = $($nombres[$actual])]").Trim()
    $tamano = switch ($r) { '1' { 'Grande' } '2' { 'Intermedia' } '3' { 'Alto' } '4' { 'Normal' } default { $actual } }
    if ($Imp.PSObject.Properties['TamanoProductos']) { $Imp.TamanoProductos = $tamano }
    else { $Imp | Add-Member -NotePropertyName 'TamanoProductos' -NotePropertyValue $tamano }
    Escribir-Ok "Letra de los platos: $($nombres[$tamano])"
}
