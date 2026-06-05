using System.Globalization;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using FluentResults;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class ManagerPaymentClient : IManagerPaymentClient
{
    private readonly Proto.ManagerPayment.ManagerPaymentClient client;

    public ManagerPaymentClient(Proto.ManagerPayment.ManagerPaymentClient client)
    {
        this.client = client;
    }

    public async Task<Result<PaymentResponse>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.ManagerPayRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                PaymentMethodId = paymentMethodId,
                Session = session.ToProtoSession(),
                BookingId = bookingId
            };
            var response = await this.client.PayAsync(request, cancellationToken: ct);
            return Result.Ok(response.ToPaymentResponse());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateSetupSessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(metadata);
        var response = await this.client.CreateSetupSessionAsync(request, cancellationToken: ct);
        return response.ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateVerifySessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(metadata);
        var response = await this.client.CreateVerifySessionAsync(request, cancellationToken: ct);
        return response.ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateHoldSessionRequest
        {
            PayerId = payerId.ToString(),
            Amount = amount.ToString(CultureInfo.InvariantCulture)
        };
        request.Metadata.Add(metadata);
        var response = await this.client.CreateHoldSessionAsync(request, cancellationToken: ct);
        return response.ToCheckoutSession();
    }

    public async Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default)
    {
        var request = new Proto.FindHeldIntentRequest
        {
            PayerId = payerId.ToString(),
            ApplicationId = applicationId
        };
        var response = await this.client.FindHeldIntentAsync(request, cancellationToken: ct);
        return response.PaymentIntentId;
    }
}
