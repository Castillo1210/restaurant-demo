# =====================================================================
#  configurar-impresora.ps1 - Configura o cambia la TICKETERA DE COCINA
#  (comandas) del sistema ya instalado: pide la IP, prueba la conexión,
#  imprime una hoja de prueba, guarda y reinicia el servicio.
#
#  Uso: clic derecho en CONFIGURAR-IMPRESORA.bat -> Ejecutar como administrador
#  (está en C:\RestaurantePOS\herramientas y en el paquete de instalación).
#
#  La ticketera de CAJA (boletas/facturas) no se configura aquí: es la
#  impresora predeterminada de Windows (ver INSTALACION_WINDOWS.md, sección 9).
# =====================================================================

param(
    [string]$Destino = 'C:\RestaurantePOS'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$Config = Join-Path $Destino 'app\appsettings.Production.json'
$Respaldos = Join-Path $Destino 'respaldos'

try {
    if (-not (Es-Administrador)) { Detener 'Ejecuta CONFIGURAR-IMPRESORA.bat como ADMINISTRADOR (clic derecho).' }
    if (-not (Test-Path $Config)) { Detener "No encuentro $Config. ¿Está instalado el sistema en $Destino?" }

    Escribir-Paso 'Ticketera de cocina (comandas)'
    $cfg = Get-Content $Config -Raw -Encoding UTF8 | ConvertFrom-Json
    Configurar-ImpresoraCocina $cfg

    # Copia de la configuración anterior en "respaldos" (carpeta solo para administradores:
    # el archivo tiene claves y no debe quedar copiado en la carpeta del programa).
    if (Test-Path $Respaldos) {
        Copy-Item $Config (Join-Path $Respaldos ('appsettings.Production.' + (Get-Date -Format 'yyyy-MM-dd_HHmm') + '.json')) -Force
    }
    Guardar-Texto $Config ($cfg | ConvertTo-Json -Depth 10)
    Escribir-Ok 'Configuración guardada'

    if (Get-Service -Name $script:NombreServicio -ErrorAction SilentlyContinue) {
        Escribir-Paso 'Reiniciando el servicio para aplicar el cambio'
        Restart-Service -Name $script:NombreServicio -Force
        if (-not (Esperar-Servicio $script:NombreServicio 'Running' 60)) { Detener 'El servicio no volvió a iniciar. Revisa los logs en app\logs.' }
        if (Probar-Sistema (Leer-Puerto $Config) 90) { Escribir-Ok 'Sistema funcionando' }
        else { Escribir-Aviso 'El servicio inició pero aún no responde; espera un minuto y revisa en el navegador.' }
    }
    Write-Host ''
    Write-Host 'Listo. Toma un pedido de prueba desde un celular: la comanda debe salir en la ticketera de cocina.' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host "No se pudo configurar la ticketera: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
