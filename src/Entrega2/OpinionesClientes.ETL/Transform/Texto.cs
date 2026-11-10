using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpinionesClientes.ETL.Transform;

/// <summary>
/// TRANSFORM: funciones de limpieza y conversión de datos.
/// </summary>
public static class Texto
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ValoresNulos =
        new(StringComparer.OrdinalIgnoreCase) { "null", "n/a", "na", "-", "none", "nan" };

    private static readonly string[] FormatosFecha =
    {
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm",
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss",
        "MM/dd/yyyy", "M/d/yyyy"
    };

    private static readonly HashSet<string> PalabrasPositivas = new()
    {
        "excelente", "bueno", "buena", "buenisimo", "encanta", "encanto", "recomiendo", "recomendado",
        "perfecto", "perfecta", "genial", "satisfecho", "satisfecha", "rapido", "rapida", "feliz",
        "mejor", "increible", "calidad", "gracias", "maravilloso", "util", "comodo", "facil"
    };

    private static readonly HashSet<string> PalabrasNegativas = new()
    {
        "malo", "mala", "pesimo", "pesima", "terrible", "horrible", "decepcion", "decepcionado",
        "decepcionada", "lento", "lenta", "defectuoso", "defectuosa", "roto", "rota", "peor",
        "problema", "problemas", "queja", "nunca", "devolver", "devolucion", "caro", "falla", "fallo"
    };

    /// <summary>Quita espacios sobrantes; devuelve null si el valor está vacío o es "null", "N/A", etc.</summary>
    public static string? Limpiar(string? valor)
    {
        if (valor is null) return null;
        var t = Espacios.Replace(valor.Trim(), " ");
        return t.Length == 0 || ValoresNulos.Contains(t) ? null : t;
    }

    /// <summary>Quita tildes y diéresis (á→a, ñ→n).</summary>
    public static string SinAcentos(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Convierte un encabezado a minúsculas, sin tildes ni espacios: "Puntaje Satisfacción" → "puntajesatisfaccion".</summary>
    public static string NormalizarClave(string s) =>
        new(SinAcentos(s).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Ids de origen en mayúscula y sin espacios: " c001 " → "C001".</summary>
    public static string NormalizarId(string s) => s.Trim().ToUpperInvariant();

    private static readonly Regex IdConPrefijo = new(@"^[A-Za-z]*0*(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Unifica los ids de clientes y productos: los archivos usan "7", "C007" o "P016"
    /// para el mismo tipo de dato. Se quita la letra y los ceros a la izquierda: "C007" → "7".
    /// </summary>
    public static string NormalizarIdReferencia(string s)
    {
        var m = IdConPrefijo.Match(s.Trim());
        return m.Success ? m.Groups[1].Value : NormalizarId(s);
    }

    /// <summary>"jUAN pérez" → "Juan Pérez".</summary>
    public static string ATitulo(string s) => Espanol.TextInfo.ToTitleCase(s.ToLower(Espanol));

    public static bool EsEmailValido(string email) => Email.IsMatch(email);

    public static bool TryParseFecha(string valor, out DateTime fecha)
    {
        if (DateTime.TryParseExact(valor, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
        {
            fecha = fecha.Date;
            return true;
        }
        return false;
    }

    /// <summary>Acepta "4" o "4.0", pero no "4.5" ni "cinco".</summary>
    public static bool TryParseEntero(string valor, out int numero)
    {
        numero = 0;
        if (!decimal.TryParse(valor.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return false;
        if (d != Math.Truncate(d)) return false;
        numero = (int)d;
        return true;
    }

    /// <summary>Unifica variantes: "positivo", "Positive", "POSITIVA" → "Positiva".</summary>
    public static string? NormalizarClasificacion(string valor) => NormalizarClave(valor) switch
    {
        "positiva" or "positivo" or "positive" => "Positiva",
        "negativa" or "negativo" or "negative" => "Negativa",
        "neutral" or "neutra" or "neutro"      => "Neutral",
        _ => null
    };

    /// <summary>Las reseñas web traen rating 1-5: 4-5 positiva, 3 neutral, 1-2 negativa.</summary>
    public static string ClasificarPorRating(int rating) =>
        rating >= 4 ? "Positiva" : rating == 3 ? "Neutral" : "Negativa";

    /// <summary>
    /// Los comentarios sociales no traen clasificación: se calcula contando
    /// palabras positivas y negativas del comentario.
    /// </summary>
    public static string ClasificarPorTexto(string comentario)
    {
        var palabras = Regex.Split(SinAcentos(comentario).ToLowerInvariant(), @"[^a-z]+");
        int positivas = palabras.Count(PalabrasPositivas.Contains);
        int negativas = palabras.Count(PalabrasNegativas.Contains);
        return positivas > negativas ? "Positiva" : negativas > positivas ? "Negativa" : "Neutral";
    }
}
