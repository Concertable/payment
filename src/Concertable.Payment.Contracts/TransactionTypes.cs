namespace Concertable.Payment.Contracts;

public static class TransactionTypes
{
    public const string Ticket = "ticket";
    public const string Settlement = "settlement";
    public const string Escrow = "escrow";
    public const string Verify = "verify";
    public const string EscrowRelease = "escrowRelease";
    public const string EscrowRefund = "escrowRefund";
    public const string ApplicationApply = "applicationApply";
    public const string ApplicationAccept = "applicationAccept";
}
