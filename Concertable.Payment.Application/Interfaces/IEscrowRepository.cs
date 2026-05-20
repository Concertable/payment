using Concertable.DataAccess;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowRepository : IIdRepository<EscrowEntity>
{
    Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default);
}
