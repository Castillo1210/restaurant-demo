using Microsoft.AspNetCore.SignalR;
using CaraNegra.Application.Common.Interfaces;

namespace CaraNegra.API.Hubs;

public class PedidosHubService : IPedidosHubService
{
    private readonly IHubContext<PedidosHub> _hubContext;

    public PedidosHubService(IHubContext<PedidosHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotificarNuevoPedido(NuevoPedidoEvent evento)
    {
        await _hubContext.Clients.Group("Mozo").SendAsync("NuevoPedido", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("NuevoPedido", evento);
        await _hubContext.Clients.Group("Cocina").SendAsync("NuevoPedido", evento);
    }

    public async Task NotificarPedidoEstadoCambiado(PedidoEstadoCambiadoEvent evento)
    {
        await _hubContext.Clients.Group("Mozo").SendAsync("PedidoEstadoCambiado", evento);
        await _hubContext.Clients.Group("Cajero").SendAsync("PedidoEstadoCambiado", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("PedidoEstadoCambiado", evento);
        await _hubContext.Clients.Group("Cocina").SendAsync("PedidoEstadoCambiado", evento);
    }

    public async Task NotificarPagoRecibido(PagoRecibidoEvent evento)
    {
        await _hubContext.Clients.Group("Cajero").SendAsync("PagoRecibido", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("PagoRecibido", evento);
        await _hubContext.Clients.Group("Mozo").SendAsync("PagoRecibido", evento);
    }

    public async Task NotificarPagoAnulado(PagoAnuladoEvent evento)
    {
        await _hubContext.Clients.Group("Cajero").SendAsync("PagoAnulado", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("PagoAnulado", evento);
        await _hubContext.Clients.Group("Mozo").SendAsync("PagoAnulado", evento);
    }

    public async Task NotificarMesaEstadoCambiado(MesaEstadoCambiadoEvent evento)
    {
        await _hubContext.Clients.Group("Mozo").SendAsync("MesaEstadoCambiado", evento);
        await _hubContext.Clients.Group("Cajero").SendAsync("MesaEstadoCambiado", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("MesaEstadoCambiado", evento);
    }

    public async Task NotificarPedidoActualizado(PedidoActualizadoEvent evento)
    {
        await _hubContext.Clients.Group("Mozo").SendAsync("PedidoActualizado", evento);
        await _hubContext.Clients.Group("Cajero").SendAsync("PedidoActualizado", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("PedidoActualizado", evento);
        await _hubContext.Clients.Group("Cocina").SendAsync("PedidoActualizado", evento);
    }

    public async Task NotificarDetalleEstadoCambiado(DetalleEstadoCambiadoEvent evento)
    {
        await _hubContext.Clients.Group("Cocina").SendAsync("DetalleEstadoCambiado", evento);
        await _hubContext.Clients.Group("Mozo").SendAsync("DetalleEstadoCambiado", evento);
        await _hubContext.Clients.Group("Cajero").SendAsync("DetalleEstadoCambiado", evento);
        await _hubContext.Clients.Group("Admin").SendAsync("DetalleEstadoCambiado", evento);
    }
}