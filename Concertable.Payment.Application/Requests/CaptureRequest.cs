namespace Concertable.Payment.Application.Requests;

internal sealed record CaptureRequest
{
    public required string PaymentIntentId { get; init; }
    public required IDictionary<string, string> Metadata { get; init; }
}
