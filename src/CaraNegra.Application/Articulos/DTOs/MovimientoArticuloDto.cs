namespace CaraNegra.Application.Articulos.DTOs;

public class MovimientoArticuloDto
{
    public int MovimientoArticuloId { get; set; }
    public int ArticuloId { get; set; }
    public string ArticuloNombre { get; set; } = string.Empty;

    // Se serializa como texto ("Entrada"/"Salida"/"Ajuste") gracias al
    // JsonStringEnumConverter global configurado en Program.cs.
    public string TipoMovimiento { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    // Stock del artículo inmediatamente después de aplicar este movimiento (como el saldo
    // corrido de un estado de cuenta), para poder auditar la evolución del stock en el tiempo.
    public int Balance { get; set; }

    public string? ReferenciaCod { get; set; }
    public string? Notas { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
}

public class CreateMovimientoArticuloDto
{
    /// <summary>
    /// "Entrada" | "Salida" | "Ajuste". Entrada suma Cantidad al stock, Salida la resta
    /// (valida que haya stock suficiente), y Ajuste fija el stock EXACTAMENTE en Cantidad
    /// (para corregir el stock tras un conteo físico), sin importar el valor anterior.
    /// </summary>
    public string TipoMovimiento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string? ReferenciaCod { get; set; }
    public string? Notas { get; set; }
}
