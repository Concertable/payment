using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class ManagerPaymentClient : IManagerPaymentOperationsClient
{
    private readonly Proto.ManagerPayment.ManagerPaymentClient client;

    public ManagerPaymentClient(Proto.ManagerPayment.ManagerPaymentClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentOutcome, ManagerPaymentError>> PayAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayAsync(
                new Proto.ManagerPayRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    Amount = amount.ToProtoMoney(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId
                },
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToManagerPaymentError(),
            ct);

    public Task<Result<PaymentOutcome, ManagerPaymentError>> PayBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayBoundCommissionAsync(
                new Proto.BoundCommissionManagerPayRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    Gross = gross.ToProtoMoney(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToManagerPaymentError(),
            ct);

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateSetupSessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return (await client.CreateSetupSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateVerifySessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return (await client.CreateVerifySessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateHoldSessionRequest
        {
            PayerId = payerId.ToString(),
            Amount = amount.ToProtoMoney()
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return (await client.CreateHoldSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public Task<Result<CheckoutSession, HoldSessionError>> CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        Money gross,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var request = new Proto.CreateBoundCommissionHoldSessionRequest
                {
                    PayerId = payerId.ToString(),
                    Gross = gross.ToProtoMoney(),
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                };
                request.Metadata.Add(new Dictionary<string, string>(metadata));
                return (await client.CreateBoundCommissionHoldSessionAsync(
                    request,
                    cancellationToken: ct)).ToCheckoutSession();
            },
            error => error.ToHoldSessionError(),
            ct);

    public async Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default)
    {
        var response = await client.FindHeldIntentAsync(
            new Proto.FindHeldIntentRequest
            {
                PayerId = payerId.ToString(),
                ApplicationId = applicationId
            },
            cancellationToken: ct);
        return response.PaymentIntentId;
    }

}
