using Concertable.Shared;

namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionRepository
{
    Task<TransactionEntity?> GetByIdAsync(int id);
    bool Exists(int id);
    Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId);
    Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task CreateAsync(TransactionEntity entity);
    Task SaveChangesAsync();
}
