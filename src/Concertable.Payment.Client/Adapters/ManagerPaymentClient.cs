using Reunion;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class ManagerPaymentClient : IManagerPaymentOperationsClient, IManagerPaymentReportingClient
{
    private readonly Proto.ManagerPayment.ManagerPaymentClient client;

    public ManagerPaymentClient(Proto.ManagerPayment.ManagerPaymentClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayUsingPaymentMethodAsync(
                Proto.ManagerPayUsingPaymentMethodRequest.Create(
                    operationId,
                    payerId,
                    payeeId,
                    amount,
                    paymentMethod,
                    session,
                    bookingId),
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToPaymentMethodChargeError(),
            ct);

    public Task<Result<PaymentOutcome, ManagerPaymentOperationError>> PayAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayAsync(
                Proto.ManagerPayRequest.Create(
                    operationId,
                    payerId,
                    payeeId,
                    amount,
                    paymentMethodId,
                    session,
                    bookingId),
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToManagerPaymentOperationError(),
            ct);

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
                Proto.ManagerPayRequest.Create(
                    payerId,
                    payeeId,
                    amount,
                    paymentMethodId,
                    session,
                    bookingId),
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
                Proto.BoundCommissionManagerPayRequest.Create(
                    payerId,
                    payeeId,
                    gross,
                    paymentMethodId,
                    session,
                    bookingId,
                    commissionBindingId,
                    externalReference,
                    stripeSetupIntentId),
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToManagerPaymentError(),
            ct);

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = Proto.CreateSetupSessionRequest.Create(payerId, metadata);
        return (await client.CreateSetupSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = Proto.CreateVerifySessionRequest.Create(payerId, metadata);
        return (await client.CreateVerifySessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = Proto.CreateHoldSessionRequest.Create(payerId, amount, metadata);
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
                var request = Proto.CreateBoundCommissionHoldSessionRequest.Create(
                    payerId,
                    gross,
                    metadata,
                    commissionBindingId,
                    externalReference,
                    stripeSetupIntentId);
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
            Proto.FindHeldIntentRequest.Create(payerId, applicationId),
            cancellationToken: ct);
        return response.PaymentIntentId;
    }

    public async Task<Money> GetTicketRevenueAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetTicketRevenueAsync(
            ToProtoRequest(payeeId, period),
            cancellationToken: ct)).ToMoney();

    public async Task<Money> GetSettlementPayoutsAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetSettlementPayoutsAsync(
            ToProtoRequest(payeeId, period),
            cancellationToken: ct)).ToMoney();

    public async Task<IReadOnlyList<MonthlyPaymentPoint>> GetTicketRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetTicketRevenueByMonthAsync(
            ToProtoRequest(payeeId, period),
            cancellationToken: ct)).Points.Select(point => point.ToMonthlyPaymentPoint()).ToList();

    public async Task<IReadOnlyList<MonthlyPaymentPoint>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        (await client.GetSettlementPayoutsByMonthAsync(
            ToProtoRequest(payeeId, period),
            cancellationToken: ct)).Points.Select(point => point.ToMonthlyPaymentPoint()).ToList();

    public async Task<IReadOnlyList<ManagerSettlement>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default) =>
        (await client.GetRecentSettlementsAsync(
            Proto.RecentSettlementsRequest.Create(ownerId, take),
            cancellationToken: ct)).Items.Select(item => item.ToManagerSettlement()).ToList();

    private static Proto.PaymentPeriodRequest ToProtoRequest(Guid payeeId, DateRange period) =>
        Proto.PaymentPeriodRequest.Create(payeeId, period);

}
