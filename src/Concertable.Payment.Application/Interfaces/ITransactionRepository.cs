using Concertable.Contracts;

namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionRepository
{
    Task<TransactionEntity?> GetByIdAsync(int id);
    bool Exists(int id);
    Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId);
    Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<SettlementTransactionEntity?> GetSettlementByCommissionAuthorizationIdAsync(
        Guid commissionAuthorizationId,
        CancellationToken ct = default);
    Task AddAsync(TransactionEntity entity, CancellationToken ct = default);
    Task CreateAsync(TransactionEntity entity);
    Task SaveChangesAsync();
}
