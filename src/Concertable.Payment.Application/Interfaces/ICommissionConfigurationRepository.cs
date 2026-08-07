using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionConfigurationRepository
    : IReadRepository<CommissionConfigurationEntity, Guid>
{
    Task<CommissionConfigurationEntity> GetOrCreateAsync(
        CommissionConfigurationEntity candidate,
        CancellationToken ct = default);
}
