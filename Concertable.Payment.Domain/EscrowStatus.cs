namespace Concertable.Payment.Domain;

public enum EscrowStatus
{
    Pending,
    Held,
    Released,
    Refunded,
    Disputed,
    Failed
}
