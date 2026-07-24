namespace Concertable.Payment.Application.Requests;

internal sealed record ReleaseRequest
{
    public required Guid PayeeId { get; init; }
    public required Money Amount { get; init; }
    public required string ChargeId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
