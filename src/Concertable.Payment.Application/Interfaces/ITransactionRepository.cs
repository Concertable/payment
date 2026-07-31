using Concertable.Contracts;
using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionRepository : IRepository<TransactionEntity>
{
    Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId);
    Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<SettlementTransactionEntity?> GetSettlementByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default);
    Task<SettlementTransactionEntity?> GetSettlementWithRefundsByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default);
    Task CreateAsync(TransactionEntity entity);
}
