using Microsoft.Extensions.DependencyInjection;
using OpinionesClientes.Application.Servicios;

namespace OpinionesClientes.Application;

public static class DependencyInjection
{
    /// <summary>Registra los servicios de la capa Application.</summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection servicios)
    {
        servicios.AddSingleton<ServicioExtraccion>();
        return servicios;
    }
}
