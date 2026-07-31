namespace Concertable.Payment.Domain.Enums;

public enum LedgerPostingType
{
    DirectSettlement,
    EscrowHold,
    EscrowRelease,
    EscrowRefund,
    DirectSettlementRefund
}
