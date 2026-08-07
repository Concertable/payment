using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionBindingRepository : IRepository<CommissionBindingEntity, Guid>
{
    Task<CommissionBindingEntity> GetOrCreateAsync(
        CommissionBindingEntity candidate,
        CancellationToken ct = default);

    Task<bool> TryConfirmReviewedGrossAsync(
        Guid bindingId,
        Money reviewedGross,
        CancellationToken ct = default);
}
