namespace Concertable.Payment.Application.Requests;

internal sealed record CaptureRequest
{
    public required string PaymentIntentId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
