using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class PaymentSessionOperationRepository : IPaymentSessionOperationRepository
{
    private readonly PaymentDbContext context;

    public PaymentSessionOperationRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<PaymentSessionOperationEntity?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken ct = default) =>
        context.PaymentSessionOperations
            .Include(operation => operation.Attempts)
            .SingleOrDefaultAsync(operation => operation.OperationId == operationId, ct);

    public Task<PaymentSessionOperationEntity?> GetByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        context.PaymentSessionOperations
            .Include(operation => operation.Attempts)
            .SingleOrDefaultAsync(
                operation => operation.Attempts.Any(attempt =>
                    attempt.ProviderObjectKind == providerObjectKind
                    && attempt.ProviderObjectId == providerObjectId),
                ct);

    public async Task<PaymentSessionReservation> ReserveInitialAsync(
        PaymentSessionSpecification specification,
        DateTimeOffset createdAt,
        CancellationToken ct = default)
    {
        var fingerprint = PaymentSessionFingerprint.Create(specification);
        var existing = await GetByOperationIdAsync(specification.OperationId, ct);
        if (existing is not null)
            return existing.EvaluateInitialReservation(fingerprint);

        var candidate = PaymentSessionOperationEntity.Create(
            specification,
            Guid.CreateVersion7(createdAt),
            createdAt);
        context.PaymentSessionOperations.Add(candidate);

        if (await context.TrySaveChangesAsync(static exception => exception.IsDuplicateKey(), ct))
            return new(PaymentSessionReservationDisposition.Created, candidate, candidate.CurrentAttempt);

        existing = await GetByOperationIdAsync(specification.OperationId, ct);
        if (existing is null)
            throw new InvalidOperationException($"Payment session operation {specification.OperationId} was not found after its reservation conflicted.");

        return existing.EvaluateInitialReservation(fingerprint);
    }

    public async Task<PaymentSessionReservation> ReserveNextAttemptAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        DateTimeOffset createdAt,
        CancellationToken ct = default)
    {
        var operation = await GetByOperationIdAsync(operationId, ct);
        if (operation is null)
            return new(PaymentSessionReservationDisposition.NotFound, null, null);

        var reservation = operation.ReserveNextAttempt(
            expectedAttemptId,
            expectedRevision,
            Guid.CreateVersion7(createdAt),
            createdAt);
        if (reservation.Disposition != PaymentSessionReservationDisposition.Created)
            return reservation;

        if (await context.TrySaveChangesAsync(
                static exception => exception is DbUpdateConcurrencyException || exception.IsDuplicateKey(),
                ct))
            return reservation;

        return await ReconcileNextAttemptAsync(
            operationId,
            expectedAttemptId,
            expectedRevision,
            createdAt,
            ct);
    }

    private async Task<PaymentSessionReservation> ReconcileNextAttemptAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        var canonical = await GetByOperationIdAsync(operationId, ct);
        if (canonical is null)
            throw new InvalidOperationException($"Payment session operation {operationId} disappeared during reservation.");

        return canonical.ReserveNextAttempt(
            expectedAttemptId,
            expectedRevision,
            Guid.CreateVersion7(createdAt),
            createdAt);
    }
}
