using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class CustomerPaymentGrpcService : CustomerPayment.CustomerPaymentBase
{
    private readonly ICustomerPaymentService customerPaymentService;

    public CustomerPaymentGrpcService(ICustomerPaymentService customerPaymentService)
    {
        this.customerPaymentService = customerPaymentService;
    }

    public override async Task<PaymentResponse> Pay(CustomerPayRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var result = await customerPaymentService.PayAsync(
            command.PayerId,
            command.ConcertId,
            command.PayeeId,
            command.Amount,
            command.Metadata,
            command.PaymentMethodId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoPaymentResponse();
    }

    public override async Task<CheckoutSessionResponse> CreatePaymentSession(CreatePaymentSessionRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();

        var session = await customerPaymentService.CreatePaymentSessionAsync(
            command.PayerId,
            command.ConcertId,
            command.PayeeId,
            command.Metadata,
            context.CancellationToken);

        return session.ToProtoCheckoutSession();
    }
}
