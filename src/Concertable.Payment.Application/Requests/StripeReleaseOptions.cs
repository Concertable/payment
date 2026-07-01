namespace Concertable.Payment.Application.Requests;

internal sealed record StripeReleaseOptions
{
    public required decimal Amount { get; init; }
    public required string ChargeId { get; init; }
    public required string DestinationStripeId { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
}
