namespace Concertable.Payment.Domain.Enums;

internal enum LedgerPostingType
{
    DirectSettlement,
    EscrowHold,
    EscrowRelease,
    EscrowRefund,
    DirectSettlementRefund
}
