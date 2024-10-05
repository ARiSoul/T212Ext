using Arisoul.T212.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Arisoul.T212.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddT212Client<TClientOptions>(this IServiceCollection services)
        where TClientOptions : class, IT212ClientOptions, new()
    {
        services.AddSingleton<IT212ClientOptions, TClientOptions>(sp =>
        {
            TClientOptions options = new();
            options.LoadOptions();

            return options;
        });

        services.AddScoped<AccountRestService>();
        services.AddScoped<PortfolioRestService>();
        services.AddScoped<InstrumentRestService>();
        services.AddScoped<HistoryRestService>();

        return services;
    }
}
