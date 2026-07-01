namespace Concertable.Payment.Application.Requests;

internal sealed record StripeRefundOptions
{
    public required decimal Amount { get; init; }
    public required string PaymentIntentId { get; init; }
    public string? TransferId { get; init; }
    public string? Reason { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
}
