using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Domain.Enums;
using OpinionesClientes.Infrastructure.Configuracion;

namespace OpinionesClientes.Infrastructure.Extractores;

/// <summary>
/// Extrae los COMENTARIOS DE REDES SOCIALES desde la API REST.
/// Usa IHttpClientFactory (el cliente ya trae la URL y la cabecera X-Api-Key),
/// recorre todas las páginas y reintenta si la API falla de forma temporal.
/// </summary>
public class ApiExtractor : IExtractor
{
    public const string NombreCliente = "ApiComentarios";

    private readonly IHttpClientFactory _fabrica;
    private readonly OpcionesApi _opciones;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(IHttpClientFactory fabrica, IOptions<OpcionesFuentes> opciones, ILogger<ApiExtractor> logger)
    {
        _fabrica = fabrica;
        _opciones = opciones.Value.Api;
        _logger = logger;
    }

    public string Nombre => "ApiExtractor (comentarios sociales)";
    public TipoFuente TipoFuente => TipoFuente.Api;

    public async Task<IReadOnlyList<OpinionCruda>> ExtraerAsync(CancellationToken cancelacion)
    {
        var cliente = _fabrica.CreateClient(NombreCliente);
        var resultado = new List<OpinionCruda>();
        var pagina = 1;
        int totalPaginas;

        do
        {
            var url = $"{_opciones.Endpoint}?pagina={pagina}&tamanoPagina={_opciones.TamanoPagina}";
            var respuesta = await ObtenerConReintentosAsync(cliente, url, cancelacion);
            totalPaginas = respuesta.TotalPaginas;

            resultado.AddRange(respuesta.Datos.Select(c => new OpinionCruda
            {
                TipoFuente = TipoFuente.Api,
                IdOrigen = c.IdComment,
                IdCliente = c.IdCliente,
                IdProducto = c.IdProducto,
                Fecha = c.Fecha,
                Comentario = c.Comentario,
                Canal = c.Fuente
            }));

            _logger.LogDebug("API página {Pagina}/{Total}: {Cantidad} comentarios", pagina, totalPaginas, respuesta.Datos.Count);
            pagina++;
        }
        while (pagina <= totalPaginas);

        _logger.LogInformation("API: {Total} comentarios leídos en {Paginas} páginas", resultado.Count, totalPaginas);
        return resultado;
    }

    // Reintentos con espera creciente (1 s, 2 s, 4 s...) ante errores temporales.
    // Un 401 (clave incorrecta) no se reintenta porque no se arreglará solo.
    private async Task<RespuestaApi> ObtenerConReintentosAsync(HttpClient cliente, string url, CancellationToken cancelacion)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                using var respuesta = await cliente.GetAsync(url, cancelacion);

                if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException("La API rechazó la clave (401). Revise Fuentes:Api:ApiKey en User Secrets.");

                respuesta.EnsureSuccessStatusCode();
                return await respuesta.Content.ReadFromJsonAsync<RespuestaApi>(cancellationToken: cancelacion)
                       ?? throw new InvalidDataException("La API devolvió una respuesta vacía.");
            }
            catch (Exception ex) when (EsTemporal(ex, cancelacion) && intento < _opciones.MaxReintentos)
            {
                var espera = TimeSpan.FromSeconds(Math.Pow(2, intento - 1));
                _logger.LogWarning("API falló (intento {Intento}/{Max}): {Mensaje}. Reintentando en {Espera} s",
                    intento, _opciones.MaxReintentos, ex.Message, espera.TotalSeconds);
                await Task.Delay(espera, cancelacion);
            }
        }
    }

    private static bool EsTemporal(Exception ex, CancellationToken cancelacion) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !cancelacion.IsCancellationRequested);

    // Forma del JSON que devuelve la API
    private sealed class RespuestaApi
    {
        public int TotalPaginas { get; set; }
        public List<ComentarioApi> Datos { get; set; } = new();
    }

    private sealed class ComentarioApi
    {
        public string? IdComment { get; set; }
        public string? IdCliente { get; set; }
        public string? IdProducto { get; set; }
        public string? Fuente { get; set; }
        public string? Fecha { get; set; }
        public string? Comentario { get; set; }
    }
}
