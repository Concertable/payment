namespace Concertable.Payment.Contracts;

public static class RefundReasonCodes
{
    public const string Duplicate = "duplicate";
    public const string Fraudulent = "fraudulent";
    public const string RequestedByPayer = "requested_by_customer";
}
