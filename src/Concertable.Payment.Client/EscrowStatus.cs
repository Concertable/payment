namespace Concertable.Payment.Client;

public enum EscrowStatus
{
    Pending,
    Held,
    Released,
    Refunded,
    Disputed,
    Failed
}
