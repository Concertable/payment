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

    public override async Task<PaymentResponse> Pay(ManagerPayRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var result = await managerPaymentService.PayAsync(
            command.PayerId,
            command.PayeeId,
            command.Amount,
            command.PaymentMethodId,
            command.Session,
            command.BookingId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoPaymentResponse();
    }

    public override async Task<CheckoutSessionResponse> CreateSetupSession(CreateSetupSessionRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var session = await managerPaymentService.CreateSetupSessionAsync(
            command.PayerId,
            command.Metadata,
            context.CancellationToken);

        return session.ToProtoCheckoutSession();
    }

    public override async Task<CheckoutSessionResponse> CreateVerifySession(CreateVerifySessionRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var session = await managerPaymentService.CreateVerifySessionAsync(
            command.PayerId,
            command.Metadata,
            context.CancellationToken);

        return session.ToProtoCheckoutSession();
    }

    public override async Task<CheckoutSessionResponse> CreateHoldSession(CreateHoldSessionRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var session = await managerPaymentService.CreateHoldSessionAsync(
            command.PayerId,
            command.Amount,
            command.Metadata,
            context.CancellationToken);

        return session.ToProtoCheckoutSession();
    }

    public override async Task<FindHeldIntentResponse> FindHeldIntent(FindHeldIntentRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var intentId = await managerPaymentService.FindHeldIntentAsync(
            command.PayerId,
            command.ApplicationId,
            context.CancellationToken);

        return new FindHeldIntentResponse { PaymentIntentId = intentId };
    }
}
