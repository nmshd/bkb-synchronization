namespace Synchronization.Infrastructure.EventBus
{
    public class EventBusConfiguration
    {
        public string ConnectionString { get; set; }
        public string RabbitMQUsername { get; set; }
        public string RabbitMQPassword { get; set; }

        public bool AzureServiceBusEnabled { get; set; }
        public int ConnectionRetryCount { get; set; }
        public string SubscriptionClientName { get; set; }
    }
}