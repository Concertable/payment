using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Client;

public interface IManagerPaymentReportingClient
{
    Task<Money> GetTicketRevenueAsync(Guid payeeId, DateRange period, CancellationToken ct = default);

    Task<Money> GetSettlementPayoutsAsync(Guid payeeId, DateRange period, CancellationToken ct = default);
}
