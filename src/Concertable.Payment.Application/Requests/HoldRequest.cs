namespace Concertable.Payment.Application.Requests;

internal sealed record HoldRequest
{
    public required Guid PayerId { get; init; }
    public required string PayerEmail { get; init; }
    public required Guid PayeeId { get; init; }
    public required Money Amount { get; init; }
    public required string PaymentMethodId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
    public required PaymentSession Session { get; init; }
}
