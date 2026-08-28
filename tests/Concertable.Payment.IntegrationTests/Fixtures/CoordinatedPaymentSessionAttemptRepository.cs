using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class CoordinatedPaymentSessionAttemptRepository : IPaymentSessionAttemptRepository
{
    private readonly IPaymentSessionAttemptRepository paymentSessionAttemptRepository;
    private readonly TaskCompletionSource savesMayProceed;
    private readonly Func<int> incrementSaveCount;

    public CoordinatedPaymentSessionAttemptRepository(
        IPaymentSessionAttemptRepository paymentSessionAttemptRepository,
        TaskCompletionSource savesMayProceed,
        Func<int> incrementSaveCount)
    {
        this.paymentSessionAttemptRepository = paymentSessionAttemptRepository;
        this.savesMayProceed = savesMayProceed;
        this.incrementSaveCount = incrementSaveCount;
    }

    public Task<PaymentSessionAttemptEntity?> GetByAttemptIdAsync(
        Guid attemptId,
        CancellationToken ct = default) =>
        paymentSessionAttemptRepository.GetByAttemptIdAsync(attemptId, ct);

    public Task<PaymentSessionAttemptEntity?> GetByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        paymentSessionAttemptRepository.GetByProviderObjectAsync(providerObjectKind, providerObjectId, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        if (incrementSaveCount() == 2)
            savesMayProceed.TrySetResult();

        await savesMayProceed.Task.WaitAsync(ct);
        await paymentSessionAttemptRepository.SaveChangesAsync(ct);
    }

    public void Detach(PaymentSessionAttemptEntity attempt) =>
        paymentSessionAttemptRepository.Detach(attempt);
}
