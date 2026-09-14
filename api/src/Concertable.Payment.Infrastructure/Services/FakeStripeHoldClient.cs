using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeHoldClient : IStripeHoldClient
{
    public Task CaptureAsync(
        string intentId,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct = default) =>
        Task.CompletedTask;
}
