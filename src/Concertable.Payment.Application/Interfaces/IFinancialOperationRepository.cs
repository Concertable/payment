namespace Concertable.Payment.Application.Interfaces;

internal interface IFinancialOperationRepository
{
    Task<FinancialOperationEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(FinancialOperationEntity operation, CancellationToken ct = default);
}
