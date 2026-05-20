using System.Globalization;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
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
        var result = await managerPaymentService.PayAsync(
            Guid.Parse(request.PayerId),
            Guid.Parse(request.PayeeId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.PaymentMethodId,
            request.Session == PaymentSessionType.OffSession ? PaymentSession.OffSession : PaymentSession.OnSession,
            request.BookingId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return MapPaymentResponse(result.Value);
    }

    public override async Task<CheckoutSessionResponse> CreateSetupSession(CreateSetupSessionRequest request, ServerCallContext context)
    {
        var session = await managerPaymentService.CreateSetupSessionAsync(
            Guid.Parse(request.PayerId),
            request.Metadata,
            context.CancellationToken);

        return MapCheckoutSession(session);
    }

    public override async Task<CheckoutSessionResponse> CreateVerifySession(CreateVerifySessionRequest request, ServerCallContext context)
    {
        var session = await managerPaymentService.CreateVerifySessionAsync(
            Guid.Parse(request.PayerId),
            request.Metadata,
            context.CancellationToken);

        return MapCheckoutSession(session);
    }

    public override async Task<CheckoutSessionResponse> CreateHoldSession(CreateHoldSessionRequest request, ServerCallContext context)
    {
        var session = await managerPaymentService.CreateHoldSessionAsync(
            Guid.Parse(request.PayerId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.Metadata,
            context.CancellationToken);

        return MapCheckoutSession(session);
    }

    public override async Task<FindHeldIntentResponse> FindHeldIntent(FindHeldIntentRequest request, ServerCallContext context)
    {
        var intentId = await managerPaymentService.FindHeldIntentAsync(
            Guid.Parse(request.PayerId),
            request.ApplicationId,
            context.CancellationToken);

        return new FindHeldIntentResponse { PaymentIntentId = intentId };
    }

    private static PaymentResponse MapPaymentResponse(Application.DTOs.PaymentResponse r) =>
        new()
        {
            RequiresAction = r.RequiresAction,
            ClientSecret = r.ClientSecret ?? "",
            TransactionId = r.TransactionId ?? ""
        };

    private static CheckoutSessionResponse MapCheckoutSession(Application.DTOs.CheckoutSession s) =>
        new() { ClientSecret = s.ClientSecret, CustomerSession = s.CustomerSession, CustomerId = s.CustomerId };
}
