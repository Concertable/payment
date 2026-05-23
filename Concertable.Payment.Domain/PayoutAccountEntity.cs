using Concertable.Kernel;

namespace Concertable.Payment.Domain;

public class PayoutAccountEntity : IIdEntity
{
    private PayoutAccountEntity() { }

    private PayoutAccountEntity(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
        Status = PayoutAccountStatus.NotVerified;
    }

    public int Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? StripeAccountId { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public PayoutAccountStatus Status { get; private set; }

    public static PayoutAccountEntity Create(Guid userId, string email) => new(userId, email);

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
