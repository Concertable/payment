namespace Concertable.Payment.Application.Requests;

internal sealed record RefundRequest
{
    public required Money Amount { get; init; }
    public required string PaymentIntentId { get; init; }
    public TransferReversal? TransferReversal { get; init; }
    public bool ReverseTransfer { get; init; }
    public string? Reason { get; init; }
    public Guid? OperationId { get; init; }
    public Guid? CommissionBindingId { get; init; }
    public required Guid RefundId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
