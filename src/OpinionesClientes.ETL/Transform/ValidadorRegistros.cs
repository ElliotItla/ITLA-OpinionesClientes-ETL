using System.Diagnostics.CodeAnalysis;
using OpinionesClientes.ETL.Extract;
using OpinionesClientes.ETL.Models;

namespace OpinionesClientes.ETL.Transform;

/// <summary>
/// TRANSFORM: valida estructura, campos obligatorios, formatos y tipos de datos,
/// y convierte cada fila válida en un objeto listo para cargar.
/// </summary>
public class ValidadorRegistros
{
    private const int LargoMaxComentario = 1000;

    /// <summary>
    /// Nombres aceptados para cada columna (ya normalizados), por si el CSV
    /// trae los encabezados en inglés o con otra forma de escribirlos.
    /// </summary>
    private static readonly Dictionary<string, string[]> Alias = new()
    {
        ["idcliente"]     = new[] { "idcliente", "idclient", "clienteid", "customerid", "idcustomer" },
        ["idproducto"]    = new[] { "idproducto", "idproduct", "productoid", "productid" },
        ["idencuesta"]    = new[] { "idopinion", "idencuesta", "idsurvey", "id" },
        ["idresena"]      = new[] { "idreview", "idresena", "idresenaweb", "id" },
        ["idcomentario"]  = new[] { "idcomment", "idcomentario", "idcomentariosocial", "id" },
        ["nombre"]        = new[] { "nombre", "name", "nombrecliente", "nombreproducto", "productname" },
        ["email"]         = new[] { "email", "correo", "correoelectronico", "mail" },
        ["categoria"]     = new[] { "categoria", "category" },
        ["fecha"]         = new[] { "fecha", "date" },
        ["comentario"]    = new[] { "comentario", "comment", "texto", "review" },
        ["clasificacion"] = new[] { "clasificacion", "classification", "sentimiento" },
        ["puntaje"]       = new[] { "puntajesatisfaccion", "puntaje", "satisfaccion", "score" },
        ["rating"]        = new[] { "rating", "calificacion", "estrellas" },
        ["redsocial"]     = new[] { "redsocial", "fuente", "source", "plataforma" },
        ["idfuente"]      = new[] { "idfuente", "idsource" },
        ["tipofuente"]    = new[] { "tipofuente", "tipo", "sourcetype" },
        ["fechacarga"]    = new[] { "fechacarga", "loaddate" }
    };

    public static readonly string[] ColumnasClientes = { "idcliente", "nombre", "email" };
    public static readonly string[] ColumnasProductos = { "idproducto", "nombre", "categoria" };
    public static readonly string[] ColumnasFuentes = { "idfuente", "tipofuente", "fechacarga" };

    public static string[] ColumnasOpinion(TipoFuente tipo) => tipo switch
    {
        TipoFuente.Encuesta => new[] { "idencuesta", "idcliente", "idproducto", "fecha", "comentario", "clasificacion", "puntaje" },
        TipoFuente.ResenaWeb => new[] { "idresena", "idcliente", "idproducto", "fecha", "comentario", "rating" },
        _ => new[] { "idcomentario", "idcliente", "idproducto", "redsocial", "fecha", "comentario" }
    };

    // ------------------------------------------------------------------ estructura

    /// <summary>Verifica que el archivo tenga todas las columnas obligatorias.</summary>
    public void ValidarEstructura(ArchivoCsv archivo, IEnumerable<string> columnas)
    {
        var faltantes = columnas.Where(c => !Alias[c].Any(archivo.Encabezados.Contains)).ToList();
        if (faltantes.Count > 0)
            throw new ExcepcionEstructuraCsv(
                $"El archivo {archivo.Nombre} no tiene las columnas obligatorias: {string.Join(", ", faltantes)}. " +
                $"Encabezados encontrados: {string.Join(", ", archivo.Encabezados)}");
    }

    // ------------------------------------------------------------------ clientes

    public bool TryValidarCliente(FilaCsv fila, [NotNullWhen(true)] out ClienteDto? cliente, out string error)
    {
        cliente = null;
        if (!FormatoValido(fila, out error)) return false;

        var errores = new List<string>();
        var id = Requerido(fila, "idcliente", errores);
        var nombre = Requerido(fila, "nombre", errores);
        var email = Requerido(fila, "email", errores);

        if (email is not null && !Texto.EsEmailValido(email))
            errores.Add($"Email con formato inválido: '{email}'");
        LargoMaximo(nombre, 150, "nombre", errores);
        LargoMaximo(email, 150, "email", errores);

        if (errores.Count > 0) { error = string.Join("; ", errores); return false; }

        cliente = new ClienteDto
        {
            IdOrigen = Texto.NormalizarIdReferencia(id!),
            Nombre = Texto.ATitulo(nombre!),
            Email = email!.ToLowerInvariant()
        };
        return true;
    }

    // ------------------------------------------------------------------ productos

    public bool TryValidarProducto(FilaCsv fila, [NotNullWhen(true)] out ProductoDto? producto, out string error)
    {
        producto = null;
        if (!FormatoValido(fila, out error)) return false;

        var errores = new List<string>();
        var id = Requerido(fila, "idproducto", errores);
        var nombre = Requerido(fila, "nombre", errores);
        var categoria = Requerido(fila, "categoria", errores);
        LargoMaximo(nombre, 150, "nombre", errores);
        LargoMaximo(categoria, 100, "categoría", errores);

        if (errores.Count > 0) { error = string.Join("; ", errores); return false; }

        producto = new ProductoDto
        {
            IdOrigen = Texto.NormalizarIdReferencia(id!),
            Nombre = nombre!,
            Categoria = Texto.ATitulo(categoria!)
        };
        return true;
    }

    // ------------------------------------------------------------------ fuentes de datos

    public bool TryValidarFuente(FilaCsv fila, [NotNullWhen(true)] out FuenteDto? fuente, out string error)
    {
        fuente = null;
        if (!FormatoValido(fila, out error)) return false;

        var errores = new List<string>();
        var id = Requerido(fila, "idfuente", errores);
        var tipo = Requerido(fila, "tipofuente", errores);
        var fechaTexto = Requerido(fila, "fechacarga", errores);
        LargoMaximo(tipo, 50, "tipo de fuente", errores);

        DateTime fecha = default;
        if (fechaTexto is not null)
        {
            if (!Texto.TryParseFecha(fechaTexto, out fecha))
                errores.Add($"Fecha de carga con formato inválido: '{fechaTexto}'");
            else if (fecha > DateTime.Today || fecha.Year < 2000)
                errores.Add($"Fecha de carga fuera de rango: '{fechaTexto}'");
        }

        if (errores.Count > 0) { error = string.Join("; ", errores); return false; }

        fuente = new FuenteDto
        {
            IdOrigen = Texto.NormalizarId(id!),
            TipoFuente = Texto.NormalizarClave(tipo!) == "csv" ? "CSV" : Texto.ATitulo(tipo!),   // "csv" → "CSV", "WEB" → "Web", "red social" → "Red Social"
            FechaCarga = fecha
        };
        return true;
    }

    // ------------------------------------------------------------------ opiniones

    public bool TryValidarOpinion(TipoFuente tipo, FilaCsv fila, [NotNullWhen(true)] out OpinionDto? opinion, out string error)
    {
        opinion = null;
        if (!FormatoValido(fila, out error)) return false;

        var errores = new List<string>();
        var campoId = tipo switch
        {
            TipoFuente.Encuesta => "idencuesta",
            TipoFuente.ResenaWeb => "idresena",
            _ => "idcomentario"
        };

        var idOrigen = Requerido(fila, campoId, errores);
        // En redes sociales el cliente puede ser anónimo
        var idCliente = tipo == TipoFuente.ComentarioSocial ? Valor(fila, "idcliente") : Requerido(fila, "idcliente", errores);
        var idProducto = Requerido(fila, "idproducto", errores);
        var fechaTexto = Requerido(fila, "fecha", errores);
        // En redes sociales el comentario es obligatorio porque de él sale la clasificación
        var comentario = tipo == TipoFuente.ComentarioSocial ? Requerido(fila, "comentario", errores) : Valor(fila, "comentario");

        DateTime fecha = default;
        if (fechaTexto is not null)
        {
            if (!Texto.TryParseFecha(fechaTexto, out fecha))
                errores.Add($"Fecha con formato inválido: '{fechaTexto}'");
            else if (fecha > DateTime.Today || fecha.Year < 2000)
                errores.Add($"Fecha fuera de rango: '{fechaTexto}'");
        }

        string? clasificacion = null, redSocial = null;
        int? puntaje = null, rating = null;

        switch (tipo)
        {
            case TipoFuente.Encuesta:
                var textoClasificacion = Requerido(fila, "clasificacion", errores);
                if (textoClasificacion is not null)
                {
                    clasificacion = Texto.NormalizarClasificacion(textoClasificacion);
                    if (clasificacion is null)
                        errores.Add($"Clasificación no reconocida: '{textoClasificacion}' (se espera Positiva, Neutral o Negativa)");
                }
                var textoPuntaje = Requerido(fila, "puntaje", errores);
                if (textoPuntaje is not null)
                {
                    if (!Texto.TryParseEntero(textoPuntaje, out var valorPuntaje))
                        errores.Add($"Tipo de dato inválido en puntaje: '{textoPuntaje}' (se espera un número entero)");
                    else if (valorPuntaje is < 1 or > 5)
                        errores.Add($"Puntaje fuera de rango: {valorPuntaje} (se espera 1 a 5)");
                    else
                        puntaje = valorPuntaje;
                }
                break;

            case TipoFuente.ResenaWeb:
                var textoRating = Requerido(fila, "rating", errores);
                if (textoRating is not null)
                {
                    if (!Texto.TryParseEntero(textoRating, out var valorRating))
                        errores.Add($"Tipo de dato inválido en rating: '{textoRating}' (se espera un número entero)");
                    else if (valorRating is < 1 or > 5)
                        errores.Add($"Rating fuera de rango: {valorRating} (se espera 1 a 5)");
                    else
                    {
                        rating = valorRating;
                        puntaje = valorRating;
                        clasificacion = Texto.ClasificarPorRating(valorRating);
                    }
                }
                break;

            case TipoFuente.ComentarioSocial:
                var textoRed = Requerido(fila, "redsocial", errores);
                if (textoRed is not null) redSocial = Texto.ATitulo(textoRed);
                LargoMaximo(redSocial, 50, "red social", errores);
                if (comentario is not null) clasificacion = Texto.ClasificarPorTexto(comentario);
                break;
        }

        LargoMaximo(idOrigen, 20, "id de origen", errores);

        if (errores.Count > 0) { error = string.Join("; ", errores); return false; }

        // Transformación: el comentario se recorta si excede el largo de la columna
        if (comentario is { Length: > LargoMaxComentario })
            comentario = comentario[..LargoMaxComentario];

        opinion = new OpinionDto
        {
            Tipo = tipo,
            IdOrigen = Texto.NormalizarId(idOrigen!),
            IdClienteOrigen = idCliente is null ? null : Texto.NormalizarIdReferencia(idCliente),
            IdProductoOrigen = Texto.NormalizarIdReferencia(idProducto!),
            Fecha = fecha,
            Comentario = comentario,
            Clasificacion = clasificacion!,
            Puntaje = puntaje,
            Rating = rating,
            RedSocial = redSocial
        };
        return true;
    }

    // ------------------------------------------------------------------ auxiliares

    private static bool FormatoValido(FilaCsv fila, out string error)
    {
        if (fila.CantidadCampos != fila.CantidadEsperada)
        {
            error = $"Formato incorrecto: se esperaban {fila.CantidadEsperada} columnas y se encontraron {fila.CantidadCampos}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static string? Valor(FilaCsv fila, string campo)
    {
        foreach (var alias in Alias[campo])
            if (fila.Campos.TryGetValue(alias, out var valor))
                return Texto.Limpiar(valor);
        return null;
    }

    private static string? Requerido(FilaCsv fila, string campo, List<string> errores)
    {
        var valor = Valor(fila, campo);
        if (valor is null) errores.Add($"Campo obligatorio vacío: {campo}");
        return valor;
    }

    private static void LargoMaximo(string? valor, int maximo, string campo, List<string> errores)
    {
        if (valor is not null && valor.Length > maximo)
            errores.Add($"El campo {campo} excede {maximo} caracteres");
    }
}
