namespace Concertable.Payment.Application.Interfaces;

internal interface IPayoutAccountRepository
{
    Task<PayoutAccountEntity?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(PayoutAccountEntity entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
