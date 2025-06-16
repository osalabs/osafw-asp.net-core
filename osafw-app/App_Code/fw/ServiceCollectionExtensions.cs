using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;

namespace osafw;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFwServices(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = FwConfig.settingsForEnvironment(configuration);
        var dbSection = (Hashtable)settings["db"];
        var main = (Hashtable)dbSection["main"];
        var connStr = (string)main["connection_string"];
        var dbType = (string)main["type"];

        services.AddScoped<DB>(_ => new DB(connStr, dbType, "main"));
        services.AddScoped<FwCache>();
        services.AddLogging();
        return services;
    }
}
