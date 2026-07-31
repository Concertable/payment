using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowRepository : IRepository<EscrowEntity>
{
    Task<EscrowEntity?> GetWithRefundsByIdAsync(int id, CancellationToken ct = default);
    Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default);
}
