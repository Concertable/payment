using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class EscrowRepository
    : Repository<EscrowEntity>, IEscrowRepository
{
    public EscrowRepository(PaymentDbContext context)
        : base(context) { }

    public Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default) =>
        context.Escrows.FirstOrDefaultAsync(e => e.BookingId == bookingId, ct);

    public Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default) =>
        context.Escrows.FirstOrDefaultAsync(e => e.ChargeId == chargeId, ct);
}
