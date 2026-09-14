using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class PaymentReportingGrpcService : PaymentReporting.PaymentReportingBase
{
    private readonly IPaymentReportingService reportingService;

    public PaymentReportingGrpcService(IPaymentReportingService reportingService)
    {
        this.reportingService = reportingService;
    }

    public override async Task<Concertable.Payment.Grpc.Money> GetPaymentRevenue(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await reportingService.GetPaymentRevenueAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoMoney();
    }

    public override async Task<Concertable.Payment.Grpc.Money> GetSettlementPayouts(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await reportingService.GetSettlementPayoutsAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoMoney();
    }

    public override async Task<MonthlyPaymentSeriesResponse> GetPaymentRevenueByMonth(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await reportingService.GetPaymentRevenueByMonthAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoResponse();
    }

    public override async Task<MonthlyPaymentSeriesResponse> GetSettlementPayoutsByMonth(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await reportingService.GetSettlementPayoutsByMonthAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoResponse();
    }

    public override async Task<SettlementReportResponse> GetRecentSettlements(
        RecentSettlementsRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await reportingService.GetRecentSettlementsAsync(
            command.OwnerId,
            command.Take,
            context.CancellationToken)).ToProtoResponse();
    }
}
