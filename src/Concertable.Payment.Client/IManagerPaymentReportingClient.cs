using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Client;

public interface IManagerPaymentReportingClient
{
    Task<Money> GetTicketRevenueAsync(Guid payeeId, DateRange period, CancellationToken ct = default);

    Task<Money> GetSettlementPayoutsAsync(Guid payeeId, DateRange period, CancellationToken ct = default);

    Task<IReadOnlyList<MonthlyPaymentPoint>> GetTicketRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);

    Task<IReadOnlyList<MonthlyPaymentPoint>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default);

    Task<IReadOnlyList<ManagerSettlement>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default);
}
