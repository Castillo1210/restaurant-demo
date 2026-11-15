namespace CaraNegra.Domain.Enums;

/// <summary>
/// Caja a la que pertenece una jornada. Cada caja abre y cierra su propia jornada, con su fondo,
/// sus movimientos y su arqueo: el restaurante (cajero / administrador) y la piscina (taquilla).
/// Se guarda como número: las jornadas anteriores a las cajas quedan en Restaurante (0).
/// </summary>
public enum CajaJornada
{
    Restaurante = 0,
    Piscina = 1
}
