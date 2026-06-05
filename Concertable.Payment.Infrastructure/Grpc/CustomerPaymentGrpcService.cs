using System.Globalization;
using Concertable.Kernel.Exceptions;
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
        var result = await customerPaymentService.PayAsync(
            Guid.Parse(request.PayerId),
            request.ConcertId,
            Guid.Parse(request.PayeeId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.Metadata,
            request.PaymentMethodId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoPaymentResponse();
    }

    public override async Task<CheckoutSessionResponse> CreatePaymentSession(CreatePaymentSessionRequest request, ServerCallContext context)
    {
        try
        {
            var session = await customerPaymentService.CreatePaymentSessionAsync(
                Guid.Parse(request.PayerId),
                request.ConcertId,
                Guid.Parse(request.PayeeId),
                request.Metadata,
                context.CancellationToken);

            return session.ToProtoCheckoutSession();
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }
}
