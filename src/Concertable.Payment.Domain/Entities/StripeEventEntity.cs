namespace Concertable.Payment.Domain.Entities;

public sealed class StripeEventEntity
{
    private StripeEventEntity() { }

    public string EventId { get; private set; } = null!;
    public DateTime EventProcessedAt { get; private set; }

    public static StripeEventEntity Create(string eventId, DateTime processedAt) => new()
    {
        EventId = eventId,
        EventProcessedAt = processedAt
    };
}
