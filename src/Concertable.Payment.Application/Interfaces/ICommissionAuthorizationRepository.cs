namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionAuthorizationRepository
{
    Task<CommissionAuthorizationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CommissionAuthorizationEntity?> GetByIdentityAsync(
        string externalReference,
        string payerReference,
        CancellationToken ct = default);

    Task AddAsync(CommissionAuthorizationEntity authorization, CancellationToken ct = default);
}
