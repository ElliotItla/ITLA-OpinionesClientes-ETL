using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Domain.Enums;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>
/// Contrato común para cualquier fuente de datos.
/// Para agregar una fuente nueva basta con crear otra clase que implemente esta interfaz
/// y registrarla; el resto del proceso no cambia (principio abierto/cerrado).
/// </summary>
public interface IExtractor
{
    string Nombre { get; }
    TipoFuente TipoFuente { get; }
    Task<IReadOnlyList<OpinionCruda>> ExtraerAsync(CancellationToken cancelacion);
}
