using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Client;

public interface IPaymentReportingClient
{
    Task<Money> GetPaymentRevenueAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<Money> GetSettlementPayoutsAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentPoint>> GetPaymentRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyPaymentPoint>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);
    Task<IReadOnlyList<PaymentSettlement>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default);
}
