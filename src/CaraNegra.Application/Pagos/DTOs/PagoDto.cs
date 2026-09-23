using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Pagos.DTOs;

public class PagoDto
{
    public int PagoId { get; set; }
    public int PedidoId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int MetodoPagoId { get; set; }
    public string MetodoPagoNombre { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public bool EstaAnulado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? AnuladoEn { get; set; }
    public int? AnuladoPorUsuarioId { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreatePagoDto
{
    public int PedidoId { get; set; }
    public decimal Monto { get; set; }
    public int MetodoPagoId { get; set; }
    public string? Referencia { get; set; }
}

public class PagoDetalleDto
{
    public int PagoId { get; set; }
    public int PedidoId { get; set; }
    public decimal Monto { get; set; }
    public int MetodoPagoId { get; set; }
    public string MetodoPagoNombre { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public DateTime CreadoEn { get; set; }
}