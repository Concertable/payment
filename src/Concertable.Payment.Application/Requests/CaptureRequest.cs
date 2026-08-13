namespace Concertable.Payment.Application.Requests;

internal sealed record CaptureRequest
{
    public required string PaymentIntentId { get; init; }
    public Guid? OperationId { get; init; }
    public Guid? CommissionBindingId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
