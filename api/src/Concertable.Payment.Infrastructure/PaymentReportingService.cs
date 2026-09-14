using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Infrastructure;

internal sealed class PaymentReportingService : IPaymentReportingService
{
    private readonly ITransactionRepository transactionRepository;

    public PaymentReportingService(ITransactionRepository transactionRepository)
    {
        this.transactionRepository = transactionRepository;
    }

    public async Task<Money> GetPaymentRevenueAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        Money.FromMinorUnits(
            await transactionRepository.GetCompletedPaymentRevenueAsync(payeeId, period, ct),
            Currency.Gbp);

    public async Task<Money> GetSettlementPayoutsAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        Money.FromMinorUnits(
            await transactionRepository.GetCompletedSettlementPayoutsAsync(payeeId, period, ct),
            Currency.Gbp);

    public Task<IReadOnlyList<MonthlyPaymentTotal>> GetPaymentRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        transactionRepository.GetCompletedPaymentRevenueByMonthAsync(payeeId, period, ct);

    public Task<IReadOnlyList<MonthlyPaymentTotal>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        transactionRepository.GetCompletedSettlementPayoutsByMonthAsync(payeeId, period, ct);

    public Task<IReadOnlyList<SettlementSummary>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default) =>
        transactionRepository.GetRecentCompletedSettlementsAsync(ownerId, take, ct);
}
