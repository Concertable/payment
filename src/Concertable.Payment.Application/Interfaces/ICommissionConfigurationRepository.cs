namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionConfigurationRepository
{
    Task<CommissionConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
