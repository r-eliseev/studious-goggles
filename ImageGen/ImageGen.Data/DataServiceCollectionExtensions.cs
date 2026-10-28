using ImageGen.Core.Storage;
using ImageGen.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the SQLite database, repositories and the image file store. Requires an <c>IAppPaths</c> registration.</summary>
    public static IServiceCollection AddImageGenData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SqliteDatabase>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<IChatMessageRepository, ChatMessageRepository>();
        services.AddSingleton<IRunRepository, RunRepository>();
        services.AddSingleton<IImageModelRepository, ImageModelRepository>();
        services.AddSingleton<IImageFileStore, FileImageStore>();
        services.AddSingleton<IChatAttachmentRepository, ChatAttachmentRepository>();
        services.AddSingleton<IImageDescriptionRepository, ImageDescriptionRepository>();
        return services;
    }
}
