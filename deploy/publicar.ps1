# =====================================================================
#  publicar.ps1 - Genera el PAQUETE DE INSTALACIÓN del Restaurante POS.
#
#  Se ejecuta en la PC de DESARROLLO (la que tiene el código), NO en la
#  del restaurante. Requiere: .NET 10 SDK, Node.js, la herramienta
#  dotnet-ef y MySQL local con la base de desarrollo (para generar el
#  script de tablas).
#
#      cd D:\restaurant-demo
#      $env:RESTAURANT_DB_CONNECTION = "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=...;"
#      powershell -ExecutionPolicy Bypass -File .\deploy\publicar.ps1
#
#  Resultado: deploy\salida\RestaurantePOS-AAAA.MM.DD-HHMM.zip
#  Ese .zip se lleva (USB) a la PC del restaurante, se descomprime y se
#  ejecuta INSTALAR.bat (o ACTUALIZAR.bat si ya estaba instalado).
# =====================================================================

param(
    [string]$Salida = (Join-Path $PSScriptRoot 'salida')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$Raiz = Split-Path -Parent $PSScriptRoot
$Frontend = Join-Path $Raiz 'cara-negra-frontend'
$Api = Join-Path $Raiz 'src\CaraNegra.API\CaraNegra.API.csproj'
$Infra = Join-Path $Raiz 'src\CaraNegra.Infrastructure'
$Version = Get-Date -Format 'yyyy.MM.dd-HHmm'
$Paquete = Join-Path $Salida 'RestaurantePOS'

try {
    Escribir-Paso "Preparando el paquete $Version"
    foreach ($cmd in @('dotnet', 'npm')) {
        if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { Detener "No encuentro '$cmd' en esta PC." }
    }
    if (-not $env:RESTAURANT_DB_CONNECTION) {
        Detener ('Define RESTAURANT_DB_CONNECTION (la base de DESARROLLO) para generar el script de tablas, p. ej.:' +
            "`n   `$env:RESTAURANT_DB_CONNECTION = `"Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=...;`"")
    }
    if (Test-Path $Paquete) { Remove-Item $Paquete -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $Paquete 'sql') -Force | Out-Null

    # ── 1. Frontend (React/Vite) ─────────────────────────────────────
    Escribir-Paso 'Compilando el frontend'
    Push-Location $Frontend
    try {
        if (-not (Test-Path 'node_modules')) { & npm install; if ($LASTEXITCODE -ne 0) { Detener 'npm install falló' } }
        # La app llama a la API con rutas relativas (/api/v1, /hubs): la sirve el mismo servicio.
        $env:VITE_API_URL = '/api/v1'
        & npm run build
        if ($LASTEXITCODE -ne 0) { Detener 'La compilación del frontend falló' }
    }
    finally {
        Remove-Item Env:\VITE_API_URL -ErrorAction SilentlyContinue
        Pop-Location
    }
    Escribir-Ok 'Frontend compilado'

    # ── 2. Backend (.NET 10, autocontenido: no hace falta instalar .NET en la PC del local) ──
    Escribir-Paso 'Publicando el backend (win-x64, autocontenido)'
    & dotnet publish $Api -c Release -r win-x64 --self-contained true -o (Join-Path $Paquete 'app') -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { Detener 'dotnet publish falló' }
    Remove-Item (Join-Path $Paquete 'app\appsettings.Development.json') -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $Frontend 'dist') (Join-Path $Paquete 'app\wwwroot') -Recurse -Force
    Escribir-Ok 'Backend publicado (con el frontend en wwwroot)'

    # ── 3. Scripts SQL ───────────────────────────────────────────────
    Escribir-Paso 'Generando el script de tablas (idempotente)'
    if (-not (Get-Command 'dotnet-ef' -ErrorAction SilentlyContinue)) {
        & dotnet tool install --global dotnet-ef
    }
    # Base NUEVA: script completo (solo CREATE TABLE..., lo más simple y seguro).
    & dotnet ef migrations script --project $Infra --startup-project (Split-Path -Parent $Api) `
        --output (Join-Path $Paquete 'sql\01-estructura-base-nueva.sql')
    if ($LASTEXITCODE -ne 0) { Detener 'No se pudo generar el script de tablas (base nueva)' }
    # Base EXISTENTE (actualizaciones): idempotente, solo aplica las migraciones que falten.
    & dotnet ef migrations script --idempotent --project $Infra --startup-project (Split-Path -Parent $Api) `
        --output (Join-Path $Paquete 'sql\01-estructura.sql')
    if ($LASTEXITCODE -ne 0) { Detener 'No se pudo generar el script de tablas (actualización)' }

    $carta = Join-Path $Raiz 'scripts\carta-la-casa-del-cuy.sql'
    if (Test-Path $carta) {
        # Sin "USE restaurant_demo": el instalador lo ejecuta sobre la base de producción.
        $texto = (Get-Content $carta -Raw -Encoding UTF8) -replace '(?m)^\s*USE\s+\w+\s*;\s*\r?\n', ''
        Guardar-Texto (Join-Path $Paquete 'sql\02-carta-la-casa-del-cuy.sql') $texto
    }
    Copy-Item (Join-Path $PSScriptRoot 'sql\limpiar-datos-de-prueba.sql') (Join-Path $Paquete 'sql\opcional-limpiar-datos-de-prueba.sql')
    Escribir-Ok 'Scripts SQL listos'

    # ── 4. Instaladores y guía ───────────────────────────────────────
    Escribir-Paso 'Copiando instaladores y guía'
    foreach ($f in @('comun.ps1', 'instalar.ps1', 'actualizar.ps1', 'desinstalar.ps1', 'respaldo-mysql.ps1', 'restaurar-respaldo.ps1',
                     'configurar-impresora.ps1', 'INSTALAR.bat', 'ACTUALIZAR.bat', 'DESINSTALAR.bat', 'RESPALDAR-AHORA.bat',
                     'CONFIGURAR-IMPRESORA.bat', 'appsettings.Production.ejemplo.json')) {
        Copy-Item (Join-Path $PSScriptRoot $f) $Paquete
    }
    Copy-Item (Join-Path $Raiz 'INSTALACION_WINDOWS.md') $Paquete
    $commit = ''
    if (Get-Command git -ErrorAction SilentlyContinue) { $commit = (& git -C $Raiz rev-parse --short HEAD 2>$null) }
    Guardar-Texto (Join-Path $Paquete 'version.txt') "$Version $commit`r`n"

    # ── 5. Zip ───────────────────────────────────────────────────────
    Escribir-Paso 'Comprimiendo'
    $zip = Join-Path $Salida "RestaurantePOS-$Version.zip"
    Compress-Archive -Path $Paquete -DestinationPath $zip -Force
    $mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host ''
    Write-Host "PAQUETE LISTO: $zip ($mb MB)" -ForegroundColor Green
    Write-Host 'Llévalo a la PC del restaurante, descomprímelo y ejecuta INSTALAR.bat (o ACTUALIZAR.bat).'
}
catch {
    Write-Host "No se pudo generar el paquete: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
