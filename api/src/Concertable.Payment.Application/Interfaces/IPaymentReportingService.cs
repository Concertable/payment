using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentReportingService
{
    Task<Money> GetPaymentRevenueAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<Money> GetSettlementPayoutsAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentTotal>> GetPaymentRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentTotal>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<SettlementSummary>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default);
}
