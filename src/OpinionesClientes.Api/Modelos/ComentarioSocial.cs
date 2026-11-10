namespace OpinionesClientes.Api.Modelos;

/// <summary>
/// Un comentario publicado en redes sociales (lo que devuelve la API).
/// </summary>
public class ComentarioSocial
{
    public string IdComment { get; set; } = "";
    public string? IdCliente { get; set; }
    public string? IdProducto { get; set; }
    public string? Fuente { get; set; }      // Facebook, Instagram, Twitter
    public string? Fecha { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>
/// Respuesta paginada: la API entrega los comentarios por páginas
/// para no enviar todo de una sola vez.
/// </summary>
public record RespuestaPaginada<T>(
    int Pagina,
    int TamanoPagina,
    int TotalRegistros,
    int TotalPaginas,
    IReadOnlyList<T> Datos);
