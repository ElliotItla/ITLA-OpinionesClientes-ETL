using System.Text;
using OpinionesClientes.ETL.Models;
using OpinionesClientes.ETL.Transform;

namespace OpinionesClientes.ETL.Extract;

/// <summary>Error en la estructura del archivo (faltan columnas, archivo vacío, etc.).</summary>
public class ExcepcionEstructuraCsv : Exception
{
    public ExcepcionEstructuraCsv(string mensaje) : base(mensaje) { }
}

/// <summary>
/// EXTRACT: lee un archivo CSV y lo convierte en filas.
/// Soporta separador coma o punto y coma, valores entre comillas
/// y saltos de línea dentro de comillas.
/// </summary>
public class LectorCsv
{
    public ArchivoCsv Leer(string ruta)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo: {Path.GetFullPath(ruta)}", ruta);

        var texto = File.ReadAllText(ruta, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(texto))
            throw new ExcepcionEstructuraCsv($"El archivo {Path.GetFileName(ruta)} está vacío.");

        var separador = DetectarSeparador(texto);
        var registros = Parsear(texto, separador)
            .Where(r => !(r.Campos.Count == 1 && string.IsNullOrWhiteSpace(r.Campos[0]))) // líneas en blanco
            .ToList();

        if (registros.Count == 0)
            throw new ExcepcionEstructuraCsv($"El archivo {Path.GetFileName(ruta)} no tiene encabezados.");

        var encabezados = registros[0].Campos.Select(Texto.NormalizarClave).ToList();
        var filas = new List<FilaCsv>();

        foreach (var (linea, campos) in registros.Skip(1))
        {
            var dic = new Dictionary<string, string>();
            for (int i = 0; i < encabezados.Count && i < campos.Count; i++)
                dic.TryAdd(encabezados[i], campos[i]);

            filas.Add(new FilaCsv
            {
                NumeroLinea = linea,
                Campos = dic,
                CantidadCampos = campos.Count,
                CantidadEsperada = encabezados.Count,
                Original = string.Join(separador, campos)
            });
        }

        return new ArchivoCsv { Nombre = Path.GetFileName(ruta), Encabezados = encabezados, Filas = filas };
    }

    private static char DetectarSeparador(string texto)
    {
        var primeraLinea = texto.Split('\n')[0];
        return primeraLinea.Count(c => c == ';') > primeraLinea.Count(c => c == ',') ? ';' : ',';
    }

    private static List<(int Linea, List<string> Campos)> Parsear(string texto, char separador)
    {
        var registros = new List<(int, List<string>)>();
        var campos = new List<string>();
        var actual = new StringBuilder();
        bool enComillas = false;
        int linea = 1, lineaInicio = 1;

        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];

            if (enComillas)
            {
                if (c == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"') { actual.Append('"'); i++; } // comilla escapada
                    else enComillas = false;
                }
                else
                {
                    if (c == '\n') linea++;
                    actual.Append(c);
                }
            }
            else if (c == '"') enComillas = true;
            else if (c == separador) { campos.Add(actual.ToString()); actual.Clear(); }
            else if (c == '\r') { /* se ignora */ }
            else if (c == '\n')
            {
                campos.Add(actual.ToString());
                actual.Clear();
                registros.Add((lineaInicio, campos));
                campos = new List<string>();
                linea++;
                lineaInicio = linea;
            }
            else actual.Append(c);
        }

        if (actual.Length > 0 || campos.Count > 0)
        {
            campos.Add(actual.ToString());
            registros.Add((lineaInicio, campos));
        }

        return registros;
    }
}
