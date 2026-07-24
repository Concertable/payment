namespace Concertable.Payment.Application.Requests;

internal sealed record RefundRequest
{
    public required Money Amount { get; init; }
    public required string PaymentIntentId { get; init; }
    public string? TransferId { get; init; }
    public string? Reason { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
