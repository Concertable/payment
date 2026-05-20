namespace Concertable.Payment.Contracts;

public enum EscrowStatus
{
    Pending,
    Held,
    Released,
    Refunded,
    Disputed,
    Failed
}
