namespace OpinionesClientes.Infrastructure.Configuracion;

// Clases que reciben la configuración de appsettings.json (patrón Options).
// Cada fuente tiene su propia sección y su interruptor "Habilitada".

public class OpcionesFuentes
{
    public const string Seccion = "Fuentes";
    public OpcionesCsv Csv { get; set; } = new();
    public OpcionesBaseDatos BaseDatos { get; set; } = new();
    public OpcionesApi Api { get; set; } = new();
}

public class OpcionesCsv
{
    public bool Habilitada { get; set; } = true;
    public string Carpeta { get; set; } = "../../data/csv";
    public List<string> Archivos { get; set; } = new();
}

public class OpcionesBaseDatos
{
    public bool Habilitada { get; set; } = true;
    public string NombreConexion { get; set; } = "ResenasOrigen";
    public string Consulta { get; set; } = "";
    public int TiempoEsperaSegundos { get; set; } = 30;
}

public class OpcionesApi
{
    public bool Habilitada { get; set; } = true;
    public string UrlBase { get; set; } = "http://localhost:5135/";
    public string Endpoint { get; set; } = "api/comentarios";
    public string ApiKey { get; set; } = "";          // se guarda en User Secrets, NO aquí
    public int TamanoPagina { get; set; } = 50;
    public int MaxReintentos { get; set; } = 3;
    public int TiempoEsperaSegundos { get; set; } = 30;
}

public class OpcionesStaging
{
    public const string Seccion = "Staging";
    public bool GuardarEnArchivo { get; set; } = true;
    public string CarpetaArchivos { get; set; } = "../../data/staging";
    public bool GuardarEnBaseDatos { get; set; } = true;
    public string NombreConexion { get; set; } = "OpinionesDW";
    public string Tabla { get; set; } = "stg.Opiniones";
    public int TamanoLote { get; set; } = 5000;
}
