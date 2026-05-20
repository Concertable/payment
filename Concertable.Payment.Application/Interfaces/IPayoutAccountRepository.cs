namespace Concertable.Payment.Application.Interfaces;

internal interface IPayoutAccountRepository
{
    Task<PayoutAccountEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PayoutAccountEntity entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
