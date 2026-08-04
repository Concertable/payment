using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;
using Functional = Concertable.Kernel.Functional;

namespace Concertable.Payment.Client.Adapters;

internal sealed class ManagerPaymentClient : IManagerPaymentClient
{
    private readonly Proto.ManagerPayment.ManagerPaymentClient client;

    public ManagerPaymentClient(Proto.ManagerPayment.ManagerPaymentClient client)
    {
        this.client = client;
    }

    public async Task<Functional.Result<PaymentOutcome, PaymentError>> ChargeAsync(
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
            var money = Money.Gbp(amount);
            var request = new Proto.ManagerPayRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = money.ToProtoMoney(),
                PaymentMethodId = paymentMethodId,
                Session = session.ToProtoSession(),
                BookingId = bookingId
            };
            var response = await this.client.PayAsync(request, cancellationToken: ct);
            return Functional.Result.Success<PaymentOutcome, PaymentError>(response.ToPaymentOutcome());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<PaymentOutcome, PaymentError>(ex.ToPaymentError());
        }
    }

    public async Task<Functional.Result<PaymentOutcome, PaymentError>> ChargeBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await client.PayBoundCommissionAsync(
                new Proto.BoundCommissionManagerPayRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    ExpectedCommissionMinor = expectedCommissionMinor,
                    ExpectedPayerTotalMinor = expectedPayerTotalMinor,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct);
            return Functional.Result.Success<PaymentOutcome, PaymentError>(response.ToPaymentOutcome());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<PaymentOutcome, PaymentError>(ex.ToPaymentError());
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
        var money = Money.Gbp(amount);
        var request = new Proto.CreateHoldSessionRequest
        {
            PayerId = payerId.ToString(),
            Amount = money.ToProtoMoney()
        };
        request.Metadata.Add(metadata);
        var response = await this.client.CreateHoldSessionAsync(request, cancellationToken: ct);
        return response.ToCheckoutSession();
    }

    public async Task<Functional.Result<CheckoutSession, CommissionError>> CreateBoundCommissionHoldAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.CreateBoundCommissionHoldSessionRequest
            {
                PayerId = payerId.ToString(),
                GrossMinor = grossMinor,
                Currency = currency.ToProtoCurrency(),
                CommissionBindingId = commissionBindingId.ToString(),
                ExternalReference = externalReference,
                ExpectedCommissionMinor = expectedCommissionMinor,
                ExpectedPayerTotalMinor = expectedPayerTotalMinor,
                StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
            };
            request.Metadata.Add(metadata);
            var response = await client.CreateBoundCommissionHoldSessionAsync(
                request,
                cancellationToken: ct);
            return Functional.Result.Success<CheckoutSession, CommissionError>(response.ToCheckoutSession());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<CheckoutSession, CommissionError>(ex.ToCommissionError());
        }
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
