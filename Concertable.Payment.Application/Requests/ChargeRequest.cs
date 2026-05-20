namespace Concertable.Payment.Application.Requests;

internal record ChargeRequest
{
    public required Guid PayerId { get; init; }
    public required string PayerEmail { get; init; }
    public required Guid PayeeId { get; init; }
    public required decimal Amount { get; init; }
    public required string PaymentMethodId { get; init; }
    public required IDictionary<string, string> Metadata { get; init; }
    public required PaymentSession Session { get; init; }
}
