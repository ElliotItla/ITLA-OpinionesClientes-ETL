using Microsoft.VisualBasic.FileIO;
using OpinionesClientes.Api.Modelos;

namespace OpinionesClientes.Api.Servicios;

/// <summary>
/// Simula la "base de datos" de la red social: lee social_comments.csv
/// una sola vez al arrancar y lo mantiene en memoria.
/// </summary>
public class RepositorioComentarios
{
    private readonly List<ComentarioSocial> _comentarios;

    public RepositorioComentarios(IConfiguration configuracion, IHostEnvironment entorno, ILogger<RepositorioComentarios> logger)
    {
        var rutaRelativa = configuracion["ApiComentarios:RutaCsv"]
            ?? throw new InvalidOperationException("Falta la configuración ApiComentarios:RutaCsv.");
        var ruta = Path.GetFullPath(Path.Combine(entorno.ContentRootPath, rutaRelativa));

        _comentarios = new List<ComentarioSocial>();

        // TextFieldParser viene incluido en .NET y respeta las comas dentro de comillas.
        using var parser = new TextFieldParser(ruta) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        parser.ReadFields(); // encabezados: IdComment,IdCliente,IdProducto,Fuente,Fecha,Comentario

        while (!parser.EndOfData)
        {
            var c = parser.ReadFields();
            if (c is null || c.Length < 6) continue;

            _comentarios.Add(new ComentarioSocial
            {
                IdComment = c[0].Trim(),
                IdCliente = string.IsNullOrWhiteSpace(c[1]) ? null : c[1].Trim(),
                IdProducto = c[2].Trim(),
                Fuente = c[3].Trim(),
                Fecha = c[4].Trim(),
                Comentario = c[5].Trim()
            });
        }

        logger.LogInformation("API de comentarios: {Total} comentarios cargados desde {Ruta}", _comentarios.Count, ruta);
    }

    public RespuestaPaginada<ComentarioSocial> ObtenerPagina(int pagina, int tamanoPagina)
    {
        var total = _comentarios.Count;
        var datos = _comentarios
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToList();

        var totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);
        return new RespuestaPaginada<ComentarioSocial>(pagina, tamanoPagina, total, totalPaginas, datos);
    }
}
