# Sistema de Análisis de Opiniones de Clientes — Proceso de carga (ETL)

Aplicación .NET 8 (Worker Service) que lee los archivos CSV del proyecto, valida y
transforma los datos, y los carga en la base de datos **OpinionesClientesDB** (SQL Server).

## Estructura

```
database/scripts/
  01_CrearBaseDatos.sql            Base de datos, tablas, PK y FK
  02_ProcedimientosAlmacenados.sql Procedimientos que usa el ETL para insertar
  03_Vistas.sql                    Vistas de detalle y resumen
  04_ConsultasValidacion.sql       Consultas que demuestran la carga correcta
data/csv/                          Archivos de entrada
docs/rechazos/                     Aquí se guarda el listado de registros rechazados
src/OpinionesClientes.ETL/
  Extract/    LectorCsv.cs              Lee los CSV
  Transform/  ValidadorRegistros.cs     Valida estructura, obligatorios, formatos y tipos
              Texto.cs                  Limpieza y conversión (fechas, nombres, clasificación)
  Load/       RepositorioOpiniones.cs   Inserta llamando a los procedimientos almacenados
  Models/                               Clases de datos y resultados
  ProcesoEtl.cs                         Orquesta el proceso y muestra el resumen
  Worker.cs                             Ejecuta el proceso automáticamente al iniciar
```

## Cómo ejecutar

1. En SSMS ejecutar, en orden: `01_CrearBaseDatos.sql`, `02_ProcedimientosAlmacenados.sql`, `03_Vistas.sql`.
2. Revisar la cadena de conexión en `src/OpinionesClientes.ETL/appsettings.json`
   (por defecto `Server=localhost` con autenticación de Windows; si usa SQL Express: `Server=localhost\SQLEXPRESS`).
3. Copiar los CSV del proyecto en `data/csv/` (si los nombres son distintos, cambiarlos en `appsettings.json`).
4. Abrir `OpinionesClientes.sln` en Visual Studio 2022 y presionar F5
   (o, desde la carpeta `src/OpinionesClientes.ETL`, ejecutar `dotnet run`).

Para repetir la carga desde cero, volver a ejecutar los scripts 01, 02 y 03.
Si se ejecuta el ETL dos veces sin borrar, la segunda vez todos los registros salen como duplicados.

## Orden de carga

Fuentes de datos → Clientes → Productos (y sus categorías) → Encuestas → Reseñas web → Comentarios sociales.
Las opiniones van al final porque dependen de clientes y productos.

## Validaciones

| Validación | Dónde |
|---|---|
| Estructura (columnas obligatorias del archivo) | ValidadorRegistros.ValidarEstructura |
| Formato de la fila (cantidad de columnas), email, fecha | ValidadorRegistros / Texto |
| Campos obligatorios vacíos | ValidadorRegistros.Requerido |
| Tipos de datos (puntaje y rating enteros, rango 1-5) | ValidadorRegistros.TryValidarOpinion |
| Duplicados dentro del archivo | HashSet en ProcesoEtl |
| Duplicados contra la base de datos | Procedimientos almacenados (IF EXISTS) |
| Integridad referencial | ProcesoEtl (cliente/producto cargados) + procedimientos + FK |
| Manejo de errores | try/catch por archivo y por fila; TRY/CATCH con transacción en SQL |

## Transformaciones

- Espacios sobrantes eliminados; valores como "null" o "N/A" se tratan como vacíos.
- Nombres de clientes y categorías en formato título; emails en minúscula.
- Ids de clientes y productos unificados: los archivos usan "7", "C007" o "P016"; se convierten a "7" y "16".
- Fechas aceptadas en varios formatos (2025-03-15, 15/03/2025, etc.).
- Clasificación unificada (positivo/Positive → Positiva).
- Reseñas web: la clasificación se calcula con el rating (4-5 Positiva, 3 Neutral, 1-2 Negativa).
- Comentarios sociales: la clasificación se calcula contando palabras positivas y negativas.
- Comentarios de más de 1000 caracteres se recortan.

## Datos

En `data/csv` están los archivos del proyecto: fuente_datos, clients, products,
surveys_part1, web_reviews y social_comments.

Resultado esperado con estos archivos:

| Archivo | Procesados | Insertados | Rechazados |
|---|---|---|---|
| fuente_datos.csv | 100 | 93 | 7 (duplicados: mismo tipo y fecha) |
| clients.csv | 500 | 500 | 0 |
| products.csv | 200 | 200 | 0 |
| surveys_part1.csv | 500 | 6 | 494 (integridad referencial) |
| web_reviews.csv | 200 | 200 | 0 |
| social_comments.csv | 200 | 200 | 0 |

En surveys_part1.csv casi todas las encuestas apuntan a clientes (hasta 9983) y productos
(hasta 997) que no existen en clients.csv (1-500) ni en products.csv (1-200), por eso se rechazan.
