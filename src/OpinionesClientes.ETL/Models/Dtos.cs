namespace OpinionesClientes.ETL.Models;

public enum TipoFuente
{
    Encuesta,
    ResenaWeb,
    ComentarioSocial
}

public class ClienteDto
{
    public required string IdOrigen { get; init; }
    public required string Nombre { get; init; }
    public required string Email { get; init; }
}

public class ProductoDto
{
    public required string IdOrigen { get; init; }
    public required string Nombre { get; init; }
    public required string Categoria { get; init; }
}

public class FuenteDto
{
    public required string IdOrigen { get; init; }
    public required string TipoFuente { get; init; }
    public required DateTime FechaCarga { get; init; }
}

/// <summary>Opinión ya validada y transformada, lista para insertar.</summary>
public class OpinionDto
{
    public required TipoFuente Tipo { get; init; }
    public required string IdOrigen { get; init; }
    public string? IdClienteOrigen { get; init; }
    public required string IdProductoOrigen { get; init; }
    public required DateTime Fecha { get; init; }
    public string? Comentario { get; init; }
    public required string Clasificacion { get; init; }
    public int? Puntaje { get; init; }
    public int? Rating { get; init; }
    public string? RedSocial { get; init; }
}
