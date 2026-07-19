using Concertable.Kernel;

namespace Concertable.Payment.Domain.Entities;

public sealed class PayoutAccountEntity : IIdEntity
{
    private PayoutAccountEntity() { }

    private PayoutAccountEntity(Guid ownerId, string email)
    {
        OwnerId = ownerId;
        Email = email;
        Status = PayoutAccountStatus.NotVerified;
    }

    public int Id { get; private set; }

    /// <summary>
    /// The opaque owner of this account's Stripe identities. Payment is tenancy-agnostic — the consumer
    /// assigns the meaning: B2B passes the owning <c>Tenant</c> id, Customer passes the buyer's user id.
    /// </summary>
    public Guid OwnerId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? StripeAccountId { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public PayoutAccountStatus Status { get; private set; }

    public static PayoutAccountEntity Create(Guid ownerId, string email) => new(ownerId, email);

    public void LinkAccount(string stripeAccountId)
    {
        StripeAccountId = stripeAccountId;
        Status = PayoutAccountStatus.Pending;
    }

    public void LinkCustomer(string stripeCustomerId)
    {
        StripeCustomerId = stripeCustomerId;
    }

    public void MarkVerified() => Status = PayoutAccountStatus.Verified;
}
