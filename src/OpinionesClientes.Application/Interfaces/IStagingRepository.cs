using OpinionesClientes.Domain.Entidades;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>Guarda lo extraído en un área temporal (archivo o tabla staging).</summary>
public interface IStagingRepository
{
    string Nombre { get; }
    Task GuardarAsync(Guid idLote, IReadOnlyList<OpinionCruda> registros, CancellationToken cancelacion);
}
