using System.Globalization;
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
            Guid.Parse(request.PayeeId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.Metadata,
            request.PaymentMethodId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return MapPaymentResponse(result.Value);
    }

    public override async Task<CheckoutSessionResponse> CreatePaymentSession(CreatePaymentSessionRequest request, ServerCallContext context)
    {
        var session = await customerPaymentService.CreatePaymentSessionAsync(
            Guid.Parse(request.PayerId),
            request.Metadata,
            context.CancellationToken);

        return new CheckoutSessionResponse
        {
            ClientSecret = session.ClientSecret,
            CustomerSession = session.CustomerSession,
            CustomerId = session.CustomerId
        };
    }

    private static PaymentResponse MapPaymentResponse(Application.DTOs.PaymentResponse r) =>
        new()
        {
            RequiresAction = r.RequiresAction,
            ClientSecret = r.ClientSecret ?? "",
            TransactionId = r.TransactionId ?? ""
        };
}
