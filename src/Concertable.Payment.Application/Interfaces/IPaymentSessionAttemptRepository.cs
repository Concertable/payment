namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionAttemptRepository
{
    Task<PaymentSessionAttemptEntity?> GetByAttemptIdAsync(
        Guid attemptId,
        CancellationToken ct = default);

    Task<PaymentSessionAttemptEntity?> GetByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    void Detach(PaymentSessionAttemptEntity attempt);
}
