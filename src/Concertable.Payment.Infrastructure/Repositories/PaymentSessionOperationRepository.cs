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

    public Task<PaymentSessionOperationEntity?> GetByReferenceAsync(
        string operationType,
        string clientReference,
        CancellationToken ct = default) =>
        context.PaymentSessionOperations
            .Include(operation => operation.Attempts)
            .SingleOrDefaultAsync(
                operation => operation.OperationType == operationType
                    && operation.ClientReference == clientReference,
                ct);

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
        PaymentSessionDefinition specification,
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

        try
        {
            await context.SaveChangesAsync(ct);
            return new(PaymentSessionReservationDisposition.Created, candidate, candidate.CurrentAttempt);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            Detach(candidate.OperationId);
            existing = await GetByOperationIdAsync(specification.OperationId, ct)
                ?? await GetByReferenceAsync(
                    specification.OperationType,
                    specification.ClientReference,
                    ct);
            if (existing is null)
                throw;

            return existing.EvaluateInitialReservation(
                PaymentSessionFingerprint.Create(
                    specification.WithOperationId(existing.OperationId)));
        }
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

        try
        {
            await context.SaveChangesAsync(ct);
            return reservation;
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReconcileNextAttemptAsync(
                operationId,
                expectedAttemptId,
                expectedRevision,
                createdAt,
                ct);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            return await ReconcileNextAttemptAsync(
                operationId,
                expectedAttemptId,
                expectedRevision,
                createdAt,
                ct);
        }
    }

    private async Task<PaymentSessionReservation> ReconcileNextAttemptAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        Detach(operationId);
        var canonical = await GetByOperationIdAsync(operationId, ct);
        if (canonical is null)
            throw new InvalidOperationException($"Payment session operation {operationId} disappeared during reservation.");

        return canonical.ReserveNextAttempt(
            expectedAttemptId,
            expectedRevision,
            Guid.CreateVersion7(createdAt),
            createdAt);
    }

    private void Detach(Guid operationId)
    {
        foreach (var entry in context.ChangeTracker.Entries()
            .Where(entry => entry.Entity switch
            {
                PaymentSessionOperationEntity operation => operation.OperationId == operationId,
                PaymentSessionAttemptEntity attempt => attempt.OperationId == operationId,
                _ => false
            })
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
