namespace Concertable.Payment.Contracts;

public static class PaymentMetadataKeys
{
    public const string Type = "type";
    public const string PayerOwnerId = "payerOwnerId";
    public const string PayerEmail = "payerEmail";
    public const string PayeeOwnerId = "payeeOwnerId";
    public const string AmountMinor = "amountMinor";
    public const string Currency = "currency";
    public const string EscrowId = "escrowId";
    public const string OperationType = "operationType";
    public const string ClientReference = "clientReference";
    public const string CommissionBindingId = "commissionBindingId";
    public const string PayeeGrossMinor = "payeeGrossMinor";
    public const string CommissionGrossMinor = "commissionGrossMinor";
    public const string CommissionNetMinor = "commissionNetMinor";
    public const string CommissionVatMinor = "commissionVatMinor";
    public const string PayerTotalMinor = "payerTotalMinor";
    public const string CumulativeGrossRefundMinor = "cumulativeGrossRefundMinor";
    public const string OperationId = "operationId";
}
