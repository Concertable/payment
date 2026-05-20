namespace Concertable.Payment.Application.Requests;

internal record RefundRequest
{
    public required decimal Amount { get; init; }
    public required string PaymentIntentId { get; init; }
    public string? TransferId { get; init; }
    public string? Reason { get; init; }
    public required IDictionary<string, string> Metadata { get; init; }
}
