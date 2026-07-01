using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowRepository : IRepository<EscrowEntity>
{
    Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default);
}
