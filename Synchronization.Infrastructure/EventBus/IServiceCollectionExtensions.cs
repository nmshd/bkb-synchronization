using Microsoft.Extensions.DependencyInjection;

namespace Synchronization.Infrastructure.EventBus;

public static class EventBusServiceCollectionExtensions
{
    public static void AddEventBus(this IServiceCollection services, EventBusConfiguration configuration)
    {
        if (configuration.AzureServiceBusEnabled)
            services.AddAzureServiceBus(options =>
            {
                options.ConnectionString = configuration.ConnectionString;
                options.SubscriptionClientName = configuration.SubscriptionClientName;
            });
        else
            services.AddRabbitMQ(options =>
            {
                options.HostName = configuration.ConnectionString;
                options.Username = configuration.RabbitMQUsername;
                options.Password = configuration.RabbitMQPassword;
                options.SubscriptionClientName = configuration.SubscriptionClientName;
                options.RetryCount = configuration.ConnectionRetryCount;
            });
    }
}
