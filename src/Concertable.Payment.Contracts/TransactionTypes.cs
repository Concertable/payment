namespace Concertable.Payment.Contracts;

public static class TransactionTypes
{
    public const string Payment = "payment";
    public const string Settlement = "settlement";
    public const string SettlementRefund = "settlementRefund";
    public const string Escrow = "escrow";
    public const string Verify = "verify";
    public const string EscrowRelease = "escrowRelease";
    public const string EscrowRefund = "escrowRefund";
}
