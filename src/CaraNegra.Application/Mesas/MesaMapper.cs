using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Domain.Entities;

namespace CaraNegra.Application.Mesas;

public static class MesaMapper
{
    public static MesaDto ToDto(Mesa m) => new()
    {
        MesaId = m.MesaId,
        NumeroMesa = m.NumeroMesa,
        Estado = m.Estado,
        SalonId = m.SalonId,
        SalonNombre = m.Salon?.Nombre ?? string.Empty,
        SalonOrden = m.Salon?.Orden ?? int.MaxValue,
        SalonActivo = m.Salon?.EstaActivo ?? true,
        CreadoEn = m.CreadoEn
    };

    /// <summary>
    /// Orden natural del código de mesa: "2" antes que "10" (un orden de texto puro pondría
    /// "10" antes que "2"). Los códigos no numéricos (p.ej. "VIP-A") van después, alfabéticos.
    /// </summary>
    public static IEnumerable<Mesa> OrdenNatural(IEnumerable<Mesa> mesas) =>
        mesas
            .OrderBy(m => m.Salon?.Orden ?? int.MaxValue)
            .ThenBy(m => m.Salon?.Nombre ?? string.Empty)
            .ThenBy(m => int.TryParse(m.NumeroMesa, out _) ? 0 : 1)
            .ThenBy(m => int.TryParse(m.NumeroMesa, out var n) ? n : 0)
            .ThenBy(m => m.NumeroMesa);
}
