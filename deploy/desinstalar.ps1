# =====================================================================
#  desinstalar.ps1 - Quita el servicio "RestaurantePOS", la regla de
#  firewall y la tarea de respaldo.
#
#  NO borra la base de datos, ni los respaldos, ni la carpeta
#  C:\RestaurantePOS (por seguridad). Si quieres borrarlos, hazlo a mano.
# =====================================================================

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')
$Servicio = $script:NombreServicio

try {
    if (-not (Es-Administrador)) { Detener 'Ejecuta como ADMINISTRADOR.' }
    if (-not (Preguntar-SiNo "¿Quitar el servicio $Servicio de esta PC? (la base de datos y los respaldos NO se borran)" $false)) {
        Write-Host 'Cancelado.'; exit 0
    }

    Escribir-Paso 'Deteniendo y eliminando el servicio'
    if (Get-Service -Name $Servicio -ErrorAction SilentlyContinue) {
        Stop-Service -Name $Servicio -Force -ErrorAction SilentlyContinue
        Esperar-Servicio $Servicio 'Stopped' 60 | Out-Null
        & sc.exe delete $Servicio | Out-Null
        Escribir-Ok 'Servicio eliminado'
    }
    else { Escribir-Aviso 'El servicio no existía' }

    Escribir-Paso 'Quitando la regla de firewall y la tarea de respaldo'
    Get-NetFirewallRule -Name 'RestaurantePOS-HTTP' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Unregister-ScheduledTask -TaskName 'RestaurantePOS - Respaldo diario' -Confirm:$false -ErrorAction SilentlyContinue
    Escribir-Ok 'Listo'

    Write-Host ''
    Write-Host 'Quedan en la PC (bórralos a mano solo si estás seguro):'
    Write-Host '  - La carpeta C:\RestaurantePOS (programa, configuración y respaldos)'
    Write-Host '  - La base de datos en MySQL (DROP DATABASE restaurante;)'
}
catch {
    Write-Host "Se detuvo: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
