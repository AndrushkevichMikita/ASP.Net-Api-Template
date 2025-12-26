namespace ApiTemplate.EventBus.Abstractions
{
    public interface ISubscriptionsProcessor
    {
        Task ProcessAsync(
            string message,
            EventMetadata eventMetadata,
            IReadOnlyCollection<SubscriptionInfo> subscriptions,
            Action commitAction,
            CancellationToken cancellationToken);
    }
}