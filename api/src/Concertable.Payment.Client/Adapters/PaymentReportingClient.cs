using Concertable.Kernel.ValueObjects;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class PaymentReportingClient : IPaymentReportingClient
{
    private readonly Proto.PaymentReporting.PaymentReportingClient client;

    public PaymentReportingClient(Proto.PaymentReporting.PaymentReportingClient client)
    {
        this.client = client;
    }

    public async Task<Money> GetPaymentRevenueAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetPaymentRevenueAsync(
            Proto.PaymentPeriodRequest.Create(payeeId, period),
            cancellationToken: ct)).ToMoney();

    public async Task<Money> GetSettlementPayoutsAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetSettlementPayoutsAsync(
            Proto.PaymentPeriodRequest.Create(payeeId, period),
            cancellationToken: ct)).ToMoney();

    public async Task<IReadOnlyList<MonthlyPaymentPoint>> GetPaymentRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetPaymentRevenueByMonthAsync(
            Proto.PaymentPeriodRequest.Create(payeeId, period),
            cancellationToken: ct)).Points.Select(point => point.ToMonthlyPaymentPoint()).ToList();

    public async Task<IReadOnlyList<MonthlyPaymentPoint>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetSettlementPayoutsByMonthAsync(
            Proto.PaymentPeriodRequest.Create(payeeId, period),
            cancellationToken: ct)).Points.Select(point => point.ToMonthlyPaymentPoint()).ToList();

    public async Task<IReadOnlyList<PaymentSettlement>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default) =>
        (await client.GetRecentSettlementsAsync(
            Proto.RecentSettlementsRequest.Create(ownerId, take),
            cancellationToken: ct)).Items.Select(item => item.ToPaymentSettlement()).ToList();
}
