namespace CaraNegra.Application.Common.Interfaces;

/// <summary>Acceso (con caché) a los datos de la empresa guardados en la base de datos.</summary>
public interface IDatosEmpresaProvider
{
    Task<DatosEmpresa> ObtenerAsync(CancellationToken cancellationToken = default);

    /// <summary>Descarta la caché (se llama después de editar los datos de la empresa).</summary>
    void Invalidar();
}
