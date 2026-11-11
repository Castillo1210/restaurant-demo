namespace CaraNegra.Application.Common;

/// <summary>Validaciones de documentos de identidad peruanos (DNI, RUC, CE).</summary>
public static class DocumentoIdentidadHelper
{
    private static readonly int[] PesosRuc = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
    private static readonly string[] PrefijosRuc = { "10", "15", "16", "17", "20" };

    public static bool EsDniValido(string? dni) =>
        !string.IsNullOrWhiteSpace(dni) && dni.Length == 8 && dni.All(char.IsDigit);

    public static bool EsCarnetExtranjeriaValido(string? ce) =>
        !string.IsNullOrWhiteSpace(ce) && ce.Length is >= 8 and <= 12 && ce.All(char.IsLetterOrDigit);

    /// <summary>
    /// RUC: 11 dígitos, prefijo válido (10, 15, 16, 17, 20) y dígito verificador correcto
    /// (módulo 11, mismo algoritmo que usa SUNAT).
    /// </summary>
    public static bool EsRucValido(string? ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 11 || !ruc.All(char.IsDigit))
            return false;

        if (!PrefijosRuc.Contains(ruc[..2]))
            return false;

        var suma = 0;
        for (var i = 0; i < 10; i++)
            suma += (ruc[i] - '0') * PesosRuc[i];

        var digito = 11 - (suma % 11);
        if (digito == 10) digito = 0;
        else if (digito == 11) digito = 1;

        return digito == ruc[10] - '0';
    }
}
