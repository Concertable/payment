namespace Concertable.Payment.Domain.Entities;

public sealed class CommissionAuthorizationClaimEntity : IGuidEntity
{
    private CommissionAuthorizationClaimEntity() { }

    private CommissionAuthorizationClaimEntity(
        Guid commissionAuthorizationId,
        CommissionAuthorizationConsumer consumer,
        DateTimeOffset claimedAt)
    {
        if (commissionAuthorizationId == Guid.Empty)
            throw new DomainException("Commission authorization id is required.");

        Id = Guid.NewGuid();
        CommissionAuthorizationId = commissionAuthorizationId;
        Consumer = consumer;
        ClaimedAt = claimedAt;
    }

    public Guid Id { get; private set; }
    public Guid CommissionAuthorizationId { get; private set; }
    public CommissionAuthorizationEntity CommissionAuthorization { get; private set; } = null!;
    public CommissionAuthorizationConsumer Consumer { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }

    public static CommissionAuthorizationClaimEntity Create(
        Guid commissionAuthorizationId,
        CommissionAuthorizationConsumer consumer,
        DateTimeOffset claimedAt) =>
        new(commissionAuthorizationId, consumer, claimedAt);
}
