using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class ManagerPaymentGrpcService : ManagerPayment.ManagerPaymentBase
{
    private readonly IManagerPaymentService managerPaymentService;

    public ManagerPaymentGrpcService(IManagerPaymentService managerPaymentService)
    {
        this.managerPaymentService = managerPaymentService;
    }

    public override async Task<PaymentResponse> PayUsingPaymentMethod(
        ManagerPayUsingPaymentMethodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await managerPaymentService.PayAsync(
            command.OperationId,
            command.PayerId,
            command.PayeeId,
            command.Amount,
            command.PaymentMethod,
            command.Session,
            command.BookingId,
            context.CancellationToken);
        return result.ValueOrRpcException().ToProtoPaymentResponse();
    }

    public override async Task<PaymentResponse> Pay(ManagerPayRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        PaymentOutcome outcome;
        if (command.OperationId is { } operationId)
        {
            outcome = (await managerPaymentService.PayAsync(
                operationId,
                command.PayerId,
                command.PayeeId,
                command.Amount,
                command.PaymentMethodId,
                command.Session,
                command.BookingId,
                context.CancellationToken)).ValueOrRpcException();
        }
        else
        {
            outcome = (await managerPaymentService.PayAsync(
                command.PayerId,
                command.PayeeId,
                command.Amount,
                command.PaymentMethodId,
                command.Session,
                command.BookingId,
                context.CancellationToken)).ValueOrRpcException();
        }

        return outcome.ToProtoPaymentResponse();
    }

    public override async Task<PaymentResponse> PayBoundCommission(
        BoundCommissionManagerPayRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await managerPaymentService.PayBoundCommissionAsync(
            command.PayerId,
            command.PayeeId,
            command.Gross,
            command.PaymentMethodId,
            command.Session,
            command.BookingId,
            command.CommissionBindingId,
            command.ExternalReference,
            command.StripeSetupIntentId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoPaymentResponse();
    }

    public override async Task<CheckoutSessionResponse> CreateSetupSession(
        CreateSetupSessionRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.CreateSetupSessionAsync(
            command.PayerId,
            command.Metadata,
            context.CancellationToken)).ToProtoCheckoutSession();
    }

    public override async Task<CheckoutSessionResponse> CreateVerifySession(
        CreateVerifySessionRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.CreateVerifySessionAsync(
            command.PayerId,
            command.Metadata,
            context.CancellationToken)).ToProtoCheckoutSession();
    }

    public override async Task<CheckoutSessionResponse> CreateHoldSession(
        CreateHoldSessionRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.CreateHoldSessionAsync(
            command.PayerId,
            command.Amount,
            command.Metadata,
            context.CancellationToken)).ToProtoCheckoutSession();
    }

    public override async Task<CheckoutSessionResponse> CreateBoundCommissionHoldSession(
        CreateBoundCommissionHoldSessionRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await managerPaymentService.CreateBoundCommissionHoldSessionAsync(
            command.PayerId,
            command.Gross,
            command.Metadata,
            command.CommissionBindingId,
            command.ExternalReference,
            command.StripeSetupIntentId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoCheckoutSession();
    }

    public override async Task<FindHeldIntentResponse> FindHeldIntent(
        FindHeldIntentRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var intentId = await managerPaymentService.FindHeldIntentAsync(
            command.PayerId,
            command.ApplicationId,
            context.CancellationToken);
        return new FindHeldIntentResponse { PaymentIntentId = intentId };
    }

    public override async Task<Concertable.Payment.Grpc.Money> GetTicketRevenue(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var amount = await managerPaymentService.GetTicketRevenueAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken);
        return amount.ToProtoMoney();
    }

    public override async Task<Concertable.Payment.Grpc.Money> GetSettlementPayouts(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var amount = await managerPaymentService.GetSettlementPayoutsAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken);
        return amount.ToProtoMoney();
    }

    public override async Task<MonthlyPaymentSeriesResponse> GetTicketRevenueByMonth(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.GetTicketRevenueByMonthAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoResponse();
    }

    public override async Task<MonthlyPaymentSeriesResponse> GetSettlementPayoutsByMonth(
        PaymentPeriodRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.GetSettlementPayoutsByMonthAsync(
            command.PayeeId,
            command.Period,
            context.CancellationToken)).ToProtoResponse();
    }

    public override async Task<SettlementReportResponse> GetRecentSettlements(
        RecentSettlementsRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        return (await managerPaymentService.GetRecentSettlementsAsync(
            command.OwnerId,
            command.Take,
            context.CancellationToken)).ToProtoResponse();
    }
}
