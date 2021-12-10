using System.Text.Json.Serialization;
using Enmeshed.BuildingBlocks.API.Extensions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.EventBus;
using Microsoft.ApplicationInsights.Extensibility;
using Synchronization.API.Extensions;
using Synchronization.API.JsonConverters;
using Synchronization.Application;
using Synchronization.Application.Extensions;
using Synchronization.Infrastructure.EventBus;
using Synchronization.Infrastructure.Persistence;

namespace Synchronization.API;

public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        _env = env;
        _configuration = configuration;
    }

    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.Configure<ApplicationOptions>(_configuration.GetSection("ApplicationOptions"));

        services.AddCustomAspNetCore(_configuration, _env, options =>
        {
            options.Authentication.Audience = "synchronization";
            options.Authentication.Authority = _configuration.GetAuthorizationConfiguration().Authority;
            options.Authentication.ValidIssuer = _configuration.GetAuthorizationConfiguration().ValidIssuer;

            options.Cors.AllowedOrigins = _configuration.GetCorsConfiguration().AllowedOrigins;
            options.Cors.ExposedHeaders = _configuration.GetCorsConfiguration().ExposedHeaders;

            options.HealthChecks.SqlConnectionString = _configuration.GetSqlDatabaseConfiguration().ConnectionString;

            options.Json.Converters.Add(new DatawalletModificationIdJsonConverter());
            options.Json.Converters.Add(new SyncRunIdJsonConverter());
            options.Json.Converters.Add(new SyncErrorIdJsonConverter());
            options.Json.Converters.Add(new ExternalEventIdJsonConverter());
            options.Json.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddCustomApplicationInsights();

        services.AddCustomFluentValidation(_ => { });

        services.AddPersistence(options =>
        {
            options.DbOptions.DbConnectionString = _configuration.GetSqlDatabaseConfiguration().ConnectionString;

            options.BlobStorageOptions.ConnectionString = _configuration.GetBlobStorageConfiguration().ConnectionString;
            options.BlobStorageOptions.ContainerName = "synchronization";
        });

        services.AddEventBus(_configuration.GetEventBusConfiguration());

        services.AddApplication();

        return services.ToAutofacServiceProvider();
    }

    public void Configure(IApplicationBuilder app, TelemetryConfiguration telemetryConfiguration)
    {
        telemetryConfiguration.DisableTelemetry = !_configuration.GetApplicationInsightsConfiguration().Enabled;

        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        eventBus.AddApplicationSubscriptions();

        app.ConfigureMiddleware(_env);
    }
}
