namespace OpinionesClientes.ETL.Models;

/// <summary>Opciones del proceso leídas de appsettings.json (sección "Etl").</summary>
public class EtlOptions
{
    public string RutaCsv { get; set; } = "../../data/csv";
    public string RutaRechazos { get; set; } = "../../docs/rechazos";
    public string ArchivoFuentes { get; set; } = "fuente_datos.csv";
    public string ArchivoClientes { get; set; } = "clients.csv";
    public string ArchivoProductos { get; set; } = "products.csv";
    public string ArchivoEncuestas { get; set; } = "surveys_part1.csv";
    public string ArchivoResenasWeb { get; set; } = "web_reviews.csv";
    public string ArchivoComentariosSociales { get; set; } = "social_comments.csv";
}
