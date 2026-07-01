using System.Globalization;
using Concertable.Kernel.Exceptions;
using Concertable.Payment.Client;
using FluentResults;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class CustomerPaymentClient : ICustomerPaymentClient
{
    private readonly Proto.CustomerPayment.CustomerPaymentClient client;

    public CustomerPaymentClient(Proto.CustomerPayment.CustomerPaymentClient client)
    {
        this.client = client;
    }

    public async Task<Result<PaymentResponse>> PayAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        decimal amount,
        IDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.CustomerPayRequest
            {
                PayerId = payerId.ToString(),
                ConcertId = concertId,
                PayeeId = payeeId.ToString(),
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                PaymentMethodId = paymentMethodId
            };
            request.Metadata.Add(metadata);
            var response = await this.client.PayAsync(request, cancellationToken: ct);
            return Result.Ok(response.ToPaymentResponse());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.CreatePaymentSessionRequest { PayerId = payerId.ToString(), ConcertId = concertId, PayeeId = payeeId.ToString() };
            request.Metadata.Add(metadata);
            var response = await this.client.CreatePaymentSessionAsync(request, cancellationToken: ct);
            return response.ToCheckoutSession();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new NotFoundException(ex.Status.Detail);
        }
    }
}
