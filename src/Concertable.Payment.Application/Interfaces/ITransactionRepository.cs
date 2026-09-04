using Concertable.Contracts;
using Concertable.DataAccess.Application;
using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionRepository : IRepository<TransactionEntity>
{
    Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId);
    Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<SettlementTransactionEntity?> GetSettlementByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default);
    Task<SettlementTransactionEntity?> GetSettlementByOperationIdAsync(
        Guid operationId,
        CancellationToken ct = default);
    Task<SettlementTransactionEntity?> ReloadSettlementByOperationIdAsync(
        Guid operationId,
        CancellationToken ct = default);
    Task<SettlementTransactionEntity?> GetSettlementWithRefundsByReferenceAsync(
        PaymentOperationReference reference,
        CancellationToken ct = default);
    Task<long> GetCompletedPaymentRevenueAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<long> GetCompletedSettlementPayoutsAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentTotal>> GetCompletedPaymentRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentTotal>> GetCompletedSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<SettlementSummary>> GetRecentCompletedSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default);
    Task CreateAsync(TransactionEntity entity);
    /// <summary>
    /// Atomically reserves <paramref name="grossMinor"/> against the settlement's cumulative gross-refund
    /// ceiling in a single conditional write. Returns <see langword="true"/> when the reservation fits
    /// within <c>PayeeGrossMinor</c> (and the settlement is complete), <see langword="false"/> when a
    /// concurrent refund already consumed the remaining capacity — the lost-update-safe replacement for
    /// an optimistic-concurrency reservation.
    /// </summary>
    Task<bool> TryReserveSettlementRefundGrossAsync(int settlementId, long grossMinor, CancellationToken ct = default);

    /// <summary>Releases a previously-reserved gross amount after its Stripe refund fails.</summary>
    Task ReleaseReservedSettlementRefundGrossAsync(int settlementId, long grossMinor, CancellationToken ct = default);
}
