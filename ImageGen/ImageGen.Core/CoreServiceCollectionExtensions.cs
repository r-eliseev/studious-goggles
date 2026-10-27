using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddImageGenCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
