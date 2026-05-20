namespace Concertable.Payment.Application.Requests;

internal sealed record StripeHoldOptions
{
    public required decimal Amount { get; init; }
    public required string PaymentMethodId { get; init; }
    public required string StripeCustomerId { get; init; }
    public required string DestinationStripeId { get; init; }
    public required string ReceiptEmail { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
}
