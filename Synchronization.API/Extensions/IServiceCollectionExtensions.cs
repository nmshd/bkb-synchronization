using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Enmeshed.BuildingBlocks.API;
using Enmeshed.BuildingBlocks.API.Mvc.JsonConverters;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.BuildingBlocks.Infrastructure.UserContext;
using Enmeshed.Tooling.JsonConverters;
using FluentValidation;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synchronization.API.ApplicationInsights.TelemetryInitializers;
using Synchronization.API.Certificates;

namespace Synchronization.API.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static void AddCustomAspNetCore(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env, Action<AspNetCoreOptions> setupOptions)
        {
            var aspNetCoreOptions = new AspNetCoreOptions();
            setupOptions?.Invoke(aspNetCoreOptions);

            services
                .AddControllers(options => options.Filters.Add(typeof(CustomExceptionFilter)))
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var firstPropertyWithError = context.ModelState.First(p => p.Value.Errors.Count > 0);
                        var nameOfPropertyWithError = firstPropertyWithError.Key;
                        var firstError = firstPropertyWithError.Value.Errors.First();
                        var firstErrorMessage = !string.IsNullOrWhiteSpace(firstError.ErrorMessage)
                            ? firstError.ErrorMessage
                            : firstError.Exception.Message;

                        return new BadRequestObjectResult(HttpError.ForProduction("error.platform.inputCannotBeParsed",
                            $"'{nameOfPropertyWithError}': {firstErrorMessage}", "")); // TODO: add docs
                    };
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                    options.JsonSerializerOptions.Converters.Add(new UrlSafeBase64ToByteArrayJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new DeviceIdJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new IdentityAddressJsonConverter());

                    foreach (var converter in aspNetCoreOptions.Json.Converters)
                    {
                        options.JsonSerializerOptions.Converters.Add(converter);
                    }

                    options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidIssuer = aspNetCoreOptions.Authentication.ValidIssuer;

                    options.TokenValidationParameters.ValidateAudience = true;
                    options.TokenValidationParameters.ValidAudience = aspNetCoreOptions.Authentication.Audience;

                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    options.TokenValidationParameters.IssuerSigningKey = JwtIssuerSigningKey.Get(configuration, env);

                    options.RequireHttpsMetadata = false;
                    options.SaveToken = aspNetCoreOptions.Authentication.SaveToken;
                });

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(aspNetCoreOptions.Cors.AllowedOrigins.ToArray())
                        .WithExposedHeaders(aspNetCoreOptions.Cors.ExposedHeaders.ToArray())
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            services.AddHealthChecks().AddSqlServer(aspNetCoreOptions.HealthChecks.SqlConnectionString);

            services.AddHttpContextAccessor();

            services.AddTransient<IUserContext, AspNetCoreUserContext>();
        }

        public static void AddCustomApplicationInsights(this IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();

            services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel());
            TelemetryDebugWriter.IsTracingDisabled = true;

            services.AddSingleton<ITelemetryInitializer, UserInformationTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, CloudRoleNameTelemetryInitializer>();

            services.ConfigureTelemetryModule<EventCounterCollectionModule>((module, _) =>
            {
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "alloc-rate"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "exception-count"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-heap-size"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "working-set"));

                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Hosting", "current-requests"));

                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "connections-duration"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "current-connections"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "connections-timed-out"));

                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-AspNetCore-Server-Kestrel", "connections-per-second"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-AspNetCore-Server-Kestrel", "current-connections"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-AspNetCore-Server-Kestrel", "request-queue-length"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-AspNetCore-Server-Kestrel", "total-tls-handshakes"));
            });
        }

        public static void AddCustomFluentValidation(this IServiceCollection services, Action<FluentValidationOptions> setupOptions)
        {
            var fluentValidationOptions = new FluentValidationOptions();
            setupOptions?.Invoke(fluentValidationOptions);

            ValidatorOptions.Global.DisplayNameResolver = (_, member, _) => member != null ? char.ToLowerInvariant(member.Name[0]) + member.Name[1..] : null;

            services.AddValidatorsFromAssemblies(fluentValidationOptions.ValidatorsAssemblies);
        }

        public class AspNetCoreOptions
        {
            public CorsOptions Cors { get; set; } = new();
            public JsonOptions Json { get; set; } = new();
            public AuthenticationOptions Authentication { get; set; } = new();
            public HealthCheckOptions HealthChecks { get; set; } = new();

            public class HealthCheckOptions
            {
                public string SqlConnectionString { get; set; }
            }

            public class CorsOptions
            {
                public List<string> AllowedOrigins { get; set; } = new();
                public List<string> ExposedHeaders { get; set; } = new();
            }

            public class JsonOptions
            {
                public List<JsonConverter> Converters { get; set; } = new();
            }

            public class AuthenticationOptions
            {
                public string Authority { get; set; }
                public string ValidIssuer { get; set; }
                public string Audience { get; set; }
                public bool SaveToken { get; set; }
            }
        }

        public class FluentValidationOptions
        {
            public ICollection<Assembly> ValidatorsAssemblies { get; set; } = new List<Assembly>();
        }
    }
}
