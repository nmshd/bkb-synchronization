using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.EventBus.Events;

namespace Synchronization.Application.IntegrationEvents.Incoming.RelationshipChangeCompleted
{
    public class RelationshipChangeCompletedIntegrationEvent : IntegrationEvent
    {
        public string ChangeId { get; private set; }
        public string RelationshipId { get; private set; }
        public string ChangeCreatedBy { get; private set; }
        public string ChangeRecipient { get; private set; }
        public string ChangeResult { get; private set; }
    }
}
