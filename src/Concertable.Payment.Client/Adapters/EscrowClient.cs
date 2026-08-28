using Reunion;
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
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.DepositAsync(
                Proto.DepositRequest.Create(
                    payerId,
                    payeeId,
                    amount,
                    paymentMethodId,
                    session,
                    bookingId),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowDepositError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
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
            async () => (await client.DepositBoundCommissionAsync(
                Proto.BoundCommissionDepositRequest.Create(
                    payerId,
                    payeeId,
                    gross,
                    paymentMethodId,
                    session,
                    bookingId,
                    commissionBindingId,
                    externalReference,
                    stripeSetupIntentId),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowDepositError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureAsync(
                Proto.CaptureRequest.Create(
                    payerId,
                    payeeId,
                    amount,
                    paymentIntentId,
                    bookingId),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowCaptureError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureBoundCommissionAsync(
                Proto.BoundCommissionCaptureRequest.Create(
                    payerId,
                    payeeId,
                    gross,
                    paymentIntentId,
                    bookingId,
                    commissionBindingId,
                    externalReference),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowCaptureError(),
            ct);

    public Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseByBookingIdAsync(
        Guid operationId,
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Transfer>, EscrowReleaseOperationError>(
            async () =>
            {
                var response = await client.ReleaseByBookingIdAsync(
                    Proto.ReleaseByBookingIdRequest.Create(operationId, bookingId),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Transfer?.TransferId)
                    ? null
                    : new Transfer(response.Transfer.TransferId);
            },
            error => error.ToEscrowReleaseOperationError(),
            ct);

    public Task<Result<Option<Transfer>, EscrowReleaseError>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Transfer>, EscrowReleaseError>(
            async () =>
            {
                var response = await client.ReleaseByBookingIdAsync(
                    Proto.ReleaseByBookingIdRequest.Create(bookingId),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Transfer?.TransferId)
                    ? null
                    : new Transfer(response.Transfer.TransferId);
            },
            error => error.ToEscrowReleaseError(),
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Refund>, EscrowRefundError>(
            async () =>
            {
                var response = await client.RefundByBookingIdAsync(
                    Proto.RefundByBookingIdRequest.Create(bookingId),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.RefundId)
                    ? null
                    : new Refund(response.Refund.RefundId);
            },
            error => error.ToEscrowRefundError(),
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        Money gross,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Refund>, EscrowRefundError>(
            async () =>
            {
                var response = await client.RefundBoundCommissionByBookingIdAsync(
                    Proto.BoundCommissionRefundByBookingIdRequest.Create(bookingId, gross),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.RefundId)
                    ? null
                    : new Refund(response.Refund.RefundId);
            },
            error => error.ToEscrowRefundError(),
            ct);

}
