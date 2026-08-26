using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class EscrowRepository
    : Repository<EscrowEntity>, IEscrowRepository
{
    private readonly PaymentDbContext context;

    public EscrowRepository(PaymentDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public Task<EscrowEntity?> GetWithRefundsByIdAsync(int id, CancellationToken ct = default) =>
        context.Escrows
            .Include(e => e.Refunds)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default) =>
        context.Escrows
            .Include(e => e.Refunds)
            .FirstOrDefaultAsync(e => e.BookingId == bookingId, ct);

    public Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default) =>
        context.Escrows
            .Include(e => e.Refunds)
            .FirstOrDefaultAsync(e => e.ChargeId == chargeId, ct);

    public Task<EscrowEntity?> GetByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default) =>
        context.Escrows
            .Include(e => e.Refunds)
            .FirstOrDefaultAsync(
            e => e.CommissionBindingId == commissionBindingId,
            ct);

    public async Task<EscrowEntity?> ReserveReleaseAsync(
        int escrowId,
        Guid operationId,
        SettlementOperationFingerprint fingerprint,
        CancellationToken ct = default)
    {
        await context.Escrows
            .Where(escrow =>
                escrow.Id == escrowId &&
                escrow.Status == EscrowStatus.Held &&
                escrow.ReleaseOperationId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(escrow => escrow.ReleaseOperationId, operationId)
                    .SetProperty(escrow => escrow.ReleaseOperationFingerprintVersion, fingerprint.Version)
                    .SetProperty(escrow => escrow.ReleaseOperationFingerprint, fingerprint.Value),
                ct);

        return await ReloadByIdAsync(escrowId, ct);
    }

    public Task<EscrowEntity?> ReloadByIdAsync(int escrowId, CancellationToken ct = default)
    {
        foreach (var entry in context.ChangeTracker.Entries<EscrowEntity>()
            .Where(entry => entry.Entity.Id == escrowId)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return context.Escrows.SingleOrDefaultAsync(escrow => escrow.Id == escrowId, ct);
    }

    public async Task<bool> TryReserveRefundGrossAsync(int escrowId, long grossMinor, CancellationToken ct = default)
    {
        var affected = await context.Escrows
            .Where(e => e.Id == escrowId
                && (e.Status == EscrowStatus.Held || e.Status == EscrowStatus.Released || e.Status == EscrowStatus.Disputed)
                && e.RefundedGrossMinor + grossMinor <= e.PayeeGrossMinor)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.RefundedGrossMinor, e => e.RefundedGrossMinor + grossMinor),
                ct);
        return affected == 1;
    }

    public Task ReleaseReservedRefundGrossAsync(int escrowId, long grossMinor, CancellationToken ct = default) =>
        context.Escrows
            .Where(e => e.Id == escrowId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.RefundedGrossMinor, e => e.RefundedGrossMinor - grossMinor),
                ct);
}
