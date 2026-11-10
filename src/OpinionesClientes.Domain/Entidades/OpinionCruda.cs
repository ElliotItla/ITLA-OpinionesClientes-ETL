using OpinionesClientes.Domain.Enums;

namespace OpinionesClientes.Domain.Entidades;

/// <summary>
/// Una opinión tal como llega de su fuente, sin transformar.
/// Las 3 fuentes se llevan a esta misma forma para guardarlas juntas en staging.
/// Todo es texto a propósito: convertir tipos y limpiar es trabajo de la Transformación.
/// </summary>
public class OpinionCruda
{
    public TipoFuente TipoFuente { get; init; }
    public string? IdOrigen { get; init; }       // IdOpinion, IdReview o IdComment
    public string? IdCliente { get; init; }
    public string? IdProducto { get; init; }
    public string? Fecha { get; init; }
    public string? Comentario { get; init; }
    public string? Clasificacion { get; init; }  // solo encuestas
    public string? Puntaje { get; init; }        // PuntajeSatisfacción (encuesta) o Rating (reseña)
    public string? Canal { get; init; }          // EncuestaInterna, Web, Instagram, Twitter...
}
