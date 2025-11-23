namespace ApiTemplate.Domain.Events
{
    public class IntegrationEvent
    {
        protected IntegrationEvent(
            IntegrationEvent parentEvent = null,
            Guid? storeGroupId = null,
            Guid? storeId = null,
            Guid? requestedByUserId = null)
        {
            Id = Guid.NewGuid();
            CreatedOn = DateTimeOffset.UtcNow;
            OriginalParentId = parentEvent?.OriginalParentId ?? Id;
            ParentId = parentEvent?.Id;
            StoreGroupId = storeGroupId;
            StoreId = storeId;
            RequestedByUserId = requestedByUserId;
        }

        public Guid? OriginalParentId { get; set; }

        public Guid? ParentId { get; set; }

        public Guid Id { get; set; }

        public Guid? StoreGroupId { get; set; }

        public Guid? StoreId { get; set; }

        public Guid? RequestedByUserId { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public int RetryAttempts { get; set; }

        public string RetryConsumerId { get; set; }

        public virtual string ComputedPartitionKey => (StoreGroupId ?? Id).ToString();

        public string DistributedTracingData { get; set; }

        public virtual IDictionary<string, object> GetAdditionalLoggingProperties()
        {
            return new Dictionary<string, object>();
        }
    }
}

