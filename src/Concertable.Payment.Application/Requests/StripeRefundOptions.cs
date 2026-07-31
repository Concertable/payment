namespace Concertable.Payment.Application.Requests;

internal sealed record StripeRefundOptions
{
    public required Money Amount { get; init; }
    public required string PaymentIntentId { get; init; }
    public TransferReversal? TransferReversal { get; init; }
    public bool ReverseTransfer { get; init; }
    public string? Reason { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
}
