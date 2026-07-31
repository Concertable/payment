using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionBindingRepository : IRepository<CommissionBindingEntity, Guid>
{
    Task<CommissionBindingEntity?> GetByIdentityAsync(
        string externalReference,
        string payerReference,
        CancellationToken ct = default);
}
