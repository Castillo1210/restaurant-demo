namespace CaraNegra.Application.Reportes.DTOs;

public class ResumenVentasDto
{
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }

    // Ventas = suma de pagos activos (no anulados) registrados en el rango.
    public decimal TotalVentas { get; set; }

    // Pedidos creados en el rango (incluye cancelados, para poder ver la tasa de cancelación).
    public int CantidadPedidos { get; set; }
    public int CantidadPedidosCancelados { get; set; }

    // Pedidos con al menos un pago activo registrado en el rango (usado para el ticket promedio).
    public int CantidadPedidosPagados { get; set; }
    public decimal TicketPromedio { get; set; }

    // Suma de (SubTotal - Total) de los pedidos no cancelados creados en el rango (Fase 7):
    // cuánto se dejó de cobrar por descuentos aplicados.
    public decimal TotalDescuentos { get; set; }

    public List<VentaPorMetodoPagoDto> VentasPorMetodoPago { get; set; } = new();
}

public class VentaPorMetodoPagoDto
{
    public string MetodoPagoNombre { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int CantidadPagos { get; set; }
}
