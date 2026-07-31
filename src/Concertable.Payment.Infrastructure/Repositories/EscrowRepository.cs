using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class EscrowRepository
    : Repository<EscrowEntity>, IEscrowRepository
{
    public EscrowRepository(PaymentDbContext context)
        : base(context) { }

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
}
