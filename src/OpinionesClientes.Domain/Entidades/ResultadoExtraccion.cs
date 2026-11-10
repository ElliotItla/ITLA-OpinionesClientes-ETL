using OpinionesClientes.Domain.Enums;

namespace OpinionesClientes.Domain.Entidades;

/// <summary>Resultado de ejecutar un extractor: cuántos registros trajo, cuánto tardó y si falló.</summary>
public record ResultadoExtraccion(
    string Extractor,
    TipoFuente TipoFuente,
    IReadOnlyList<OpinionCruda> Registros,
    TimeSpan Duracion,
    string? Error = null)
{
    public bool Exitoso => Error is null;
}
