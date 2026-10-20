/* =============================================================
   Proyecto : Sistema de Análisis de Opiniones de Clientes
   Script 01: Creación de la base de datos, tablas, PK y FK
   Motor    : SQL Server
   ============================================================= */

IF DB_ID('OpinionesClientesDB') IS NULL
    CREATE DATABASE OpinionesClientesDB;
GO

USE OpinionesClientesDB;
GO

/* Si las tablas ya existen se eliminan (de hijas a padres) para recrearlas */
DROP TABLE IF EXISTS dbo.ComentarioSocial;
DROP TABLE IF EXISTS dbo.ResenaWeb;
DROP TABLE IF EXISTS dbo.Encuesta;
DROP TABLE IF EXISTS dbo.Opinion;
DROP TABLE IF EXISTS dbo.Clasificacion;
DROP TABLE IF EXISTS dbo.FuenteDatos;
DROP TABLE IF EXISTS dbo.Cliente;
DROP TABLE IF EXISTS dbo.Producto;
DROP TABLE IF EXISTS dbo.Categoria;
GO

CREATE TABLE dbo.Categoria (
    IdCategoria INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categoria PRIMARY KEY,
    Nombre      NVARCHAR(100)     NOT NULL
);

CREATE TABLE dbo.Producto (
    IdProducto  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Producto PRIMARY KEY,
    Nombre      NVARCHAR(150)     NOT NULL,
    IdCategoria INT               NOT NULL
        CONSTRAINT FK_Producto_Categoria REFERENCES dbo.Categoria (IdCategoria)
);

CREATE TABLE dbo.Cliente (
    IdCliente INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cliente PRIMARY KEY,
    Nombre    NVARCHAR(150)     NOT NULL,
    Email     NVARCHAR(150)     NOT NULL
);

CREATE TABLE dbo.FuenteDatos (
    IdFuente   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuenteDatos PRIMARY KEY,
    TipoFuente NVARCHAR(50)      NOT NULL,
    FechaCarga DATETIME2(0)      NOT NULL CONSTRAINT DF_FuenteDatos_FechaCarga DEFAULT SYSDATETIME()
);

CREATE TABLE dbo.Clasificacion (
    IdClasificacion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clasificacion PRIMARY KEY,
    Nombre          NVARCHAR(20)      NOT NULL
);

CREATE TABLE dbo.Opinion (
    IdOpinion           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Opinion PRIMARY KEY,
    IdCliente           INT               NULL      -- puede faltar en comentarios de redes sociales
        CONSTRAINT FK_Opinion_Cliente REFERENCES dbo.Cliente (IdCliente),
    IdProducto          INT               NOT NULL
        CONSTRAINT FK_Opinion_Producto REFERENCES dbo.Producto (IdProducto),
    IdFuente            INT               NOT NULL
        CONSTRAINT FK_Opinion_FuenteDatos REFERENCES dbo.FuenteDatos (IdFuente),
    IdClasificacion     INT               NOT NULL
        CONSTRAINT FK_Opinion_Clasificacion REFERENCES dbo.Clasificacion (IdClasificacion),
    Fecha               DATE              NOT NULL,
    Comentario          NVARCHAR(1000)    NULL,
    PuntajeSatisfaccion INT               NULL      -- los comentarios sociales no traen puntaje
);

CREATE TABLE dbo.Encuesta (
    IdOpinion        INT          NOT NULL CONSTRAINT PK_Encuesta PRIMARY KEY
        CONSTRAINT FK_Encuesta_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdEncuestaOrigen NVARCHAR(20) NOT NULL
);

CREATE TABLE dbo.ResenaWeb (
    IdOpinion      INT          NOT NULL CONSTRAINT PK_ResenaWeb PRIMARY KEY
        CONSTRAINT FK_ResenaWeb_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdResenaOrigen NVARCHAR(20) NOT NULL,
    Rating         INT          NOT NULL
);

CREATE TABLE dbo.ComentarioSocial (
    IdOpinion          INT          NOT NULL CONSTRAINT PK_ComentarioSocial PRIMARY KEY
        CONSTRAINT FK_ComentarioSocial_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdComentarioOrigen NVARCHAR(20) NOT NULL,
    RedSocial          NVARCHAR(50) NOT NULL
);
GO

/* Datos de catálogo fijos */
INSERT INTO dbo.Clasificacion (Nombre) VALUES (N'Positiva'), (N'Neutral'), (N'Negativa');
GO
