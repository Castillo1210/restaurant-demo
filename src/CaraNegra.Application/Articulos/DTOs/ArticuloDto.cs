namespace CaraNegra.Application.Articulos.DTOs;

public class ArticuloDto
{
    public int ArticuloId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public bool Activo { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
}

public class CreateArticuloDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }

    // Stock con el que arranca el artículo (p.ej. el conteo físico inicial). A partir de
    // aquí, el stock solo cambia registrando movimientos (entrada/salida/ajuste), nunca
    // editando el artículo directamente — así queda todo el historial auditado.
    public int StockInicial { get; set; }
}

public class UpdateArticuloDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public bool Activo { get; set; }

    // Nota: intencionalmente no incluye Stock. El stock se modifica únicamente a través de
    // RegistrarMovimientoArticuloCommand, para que cada cambio quede registrado en
    // MovimientoArticulo con quién, cuándo y por qué.
}
