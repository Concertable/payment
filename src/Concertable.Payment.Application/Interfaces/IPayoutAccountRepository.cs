using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPayoutAccountRepository : IRepository<PayoutAccountEntity>
{
    Task<PayoutAccountEntity?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
}
