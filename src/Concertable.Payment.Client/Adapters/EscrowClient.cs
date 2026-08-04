using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class EscrowClient : IEscrowOperationsClient
{
    private readonly Proto.Escrow.EscrowClient client;

    public EscrowClient(Proto.Escrow.EscrowClient client)
    {
        this.client = client;
    }

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.DepositAsync(
                new Proto.DepositRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    Amount = Money.Gbp(amount).ToProtoMoney(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId
                },
                cancellationToken: ct)).ToEscrowDeposit(),
            EscrowDepositError.FromCode,
            ct);

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
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
            async () => (await client.DepositBoundCommissionAsync(
                new Proto.BoundCommissionDepositRequest
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
                cancellationToken: ct)).ToEscrowDeposit(),
            EscrowDepositError.FromCode,
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureAsync(
                new Proto.CaptureRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    Amount = Money.Gbp(amount).ToProtoMoney(),
                    PaymentIntentId = paymentIntentId,
                    BookingId = bookingId
                },
                cancellationToken: ct)).ToEscrowDeposit(),
            EscrowCaptureError.FromCode,
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureBoundCommissionAsync(
                new Proto.BoundCommissionCaptureRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    PaymentIntentId = paymentIntentId,
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference
                },
                cancellationToken: ct)).ToEscrowDeposit(),
            EscrowCaptureError.FromCode,
            ct);

    public Task<Result<Option<Transfer>, EscrowReleaseError>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var response = await client.ReleaseByBookingIdAsync(
                    new Proto.ReleaseByBookingIdRequest { BookingId = bookingId },
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Transfer?.TransferId)
                    ? Option.None<Transfer>()
                    : Option.Some(new Transfer(response.Transfer.TransferId));
            },
            EscrowReleaseError.FromCode,
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var response = await client.RefundByBookingIdAsync(
                    new Proto.RefundByBookingIdRequest { BookingId = bookingId },
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.RefundId)
                    ? Option.None<Refund>()
                    : Option.Some(new Refund(response.Refund.RefundId));
            },
            EscrowRefundError.FromCode,
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var response = await client.RefundBoundCommissionByBookingIdAsync(
                    new Proto.BoundCommissionRefundByBookingIdRequest
                    {
                        BookingId = bookingId,
                        GrossMinor = grossMinor,
                        Currency = currency.ToProtoCurrency()
                    },
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.RefundId)
                    ? Option.None<Refund>()
                    : Option.Some(new Refund(response.Refund.RefundId));
            },
            EscrowRefundError.FromCode,
            ct);

}
