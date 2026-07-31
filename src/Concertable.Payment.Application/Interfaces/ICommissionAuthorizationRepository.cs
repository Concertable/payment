using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionAuthorizationRepository : IRepository<CommissionAuthorizationEntity, Guid>
{
    Task<CommissionAuthorizationEntity?> GetByIdentityAsync(
        string externalReference,
        string payerReference,
        CancellationToken ct = default);
}
