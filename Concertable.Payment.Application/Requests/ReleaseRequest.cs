namespace Concertable.Payment.Application.Requests;

internal record ReleaseRequest
{
    public required Guid PayeeId { get; init; }
    public required decimal Amount { get; init; }
    public required string ChargeId { get; init; }
    public required IDictionary<string, string> Metadata { get; init; }
}
