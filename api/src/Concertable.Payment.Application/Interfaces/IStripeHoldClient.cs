namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeHoldClient
{
    Task CaptureAsync(
        string intentId,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct = default);
}
