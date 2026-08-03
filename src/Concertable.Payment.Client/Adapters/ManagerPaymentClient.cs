using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class ManagerPaymentClient : IManagerPaymentOperationsClient, IManagerPaymentClient
{
    private readonly Proto.ManagerPayment.ManagerPaymentClient client;

    public ManagerPaymentClient(Proto.ManagerPayment.ManagerPaymentClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentOutcome, ManagerPaymentError>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
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
                    Amount = Money.Gbp(amount).ToProtoMoney(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId
                },
                cancellationToken: ct)).ToPaymentOutcome(),
            ManagerPaymentError.FromCode,
            ct);

    public Task<Result<PaymentOutcome, ManagerPaymentError>> PayBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
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
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct)).ToPaymentOutcome(),
            ManagerPaymentError.FromCode,
            ct);

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateSetupSessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(metadata);
        return (await client.CreateSetupSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreateVerifySessionRequest { PayerId = payerId.ToString() };
        request.Metadata.Add(metadata);
        return (await client.CreateVerifySessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
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
            Amount = Money.Gbp(amount).ToProtoMoney()
        };
        request.Metadata.Add(metadata);
        return (await client.CreateHoldSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public Task<Result<CheckoutSession, HoldSessionError>> CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IDictionary<string, string> metadata,
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
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                };
                request.Metadata.Add(metadata);
                return (await client.CreateBoundCommissionHoldSessionAsync(
                    request,
                    cancellationToken: ct)).ToCheckoutSession();
            },
            HoldSessionError.FromCode,
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

    async Task<FluentResults.Result<PaymentOutcome>> IManagerPaymentClient.PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct) =>
        (await PayAsync(payerId, payeeId, amount, paymentMethodId, session, bookingId, ct)).ToLegacy();

    async Task<FluentResults.Result<PaymentOutcome>> IManagerPaymentClient.PayBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct) =>
        (await PayBoundCommissionAsync(
            payerId,
            payeeId,
            grossMinor,
            currency,
            paymentMethodId,
            session,
            bookingId,
            commissionBindingId,
            externalReference,
            stripeSetupIntentId,
            ct)).ToLegacy();

    async Task<FluentResults.Result<CheckoutSession>> IManagerPaymentClient.CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct) =>
        (await CreateBoundCommissionHoldSessionAsync(
            payerId,
            grossMinor,
            currency,
            metadata,
            commissionBindingId,
            externalReference,
            stripeSetupIntentId,
            ct)).ToLegacy();
}
