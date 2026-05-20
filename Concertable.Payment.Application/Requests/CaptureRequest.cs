namespace Concertable.Payment.Application.Requests;

internal record CaptureRequest
{
    public required string PaymentIntentId { get; init; }
    public required IDictionary<string, string> Metadata { get; init; }
}
