using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class PaymentSessionAttemptRepository : IPaymentSessionAttemptRepository
{
    private readonly PaymentDbContext context;

    public PaymentSessionAttemptRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<PaymentSessionAttemptEntity?> GetByAttemptIdAsync(
        Guid attemptId,
        CancellationToken ct = default) =>
        context.PaymentSessionAttempts.SingleOrDefaultAsync(attempt => attempt.AttemptId == attemptId, ct);

    public Task<PaymentSessionAttemptEntity?> GetByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        context.PaymentSessionAttempts.SingleOrDefaultAsync(
            attempt => attempt.ProviderObjectKind == providerObjectKind
                && attempt.ProviderObjectId == providerObjectId,
            ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);

    public void Detach(PaymentSessionAttemptEntity attempt) =>
        context.Entry(attempt).State = EntityState.Detached;
}
