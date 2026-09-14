using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Reunion;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class EscrowClient : IEscrowOperationsClient
{
    private readonly Proto.Escrow.EscrowClient client;

    public EscrowClient(Proto.Escrow.EscrowClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentSessionDescriptor, PaymentOperationError>> AuthorizeAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.AuthorizeAsync(
                Proto.AuthorizeEscrowRequest.Create(
                    operationId,
                    reference,
                    payerId,
                    payeeId,
                    amount),
                cancellationToken: ct)).ToPaymentSessionDescriptor(),
            error => error.ToPaymentOperationError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.DepositAsync(
                Proto.DepositRequest.Create(
                    operationId,
                    reference,
                    payerId,
                    payeeId,
                    amount,
                    paymentMethod,
                    session),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowDepositError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.DepositBoundCommissionAsync(
                Proto.BoundCommissionDepositRequest.Create(
                    reference,
                    payerId,
                    payeeId,
                    gross,
                    paymentMethod,
                    session,
                    commissionBindingId,
                    externalReference),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowDepositError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference authorization,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureAsync(
                Proto.CaptureRequest.Create(
                    operationId,
                    reference,
                    payerId,
                    payeeId,
                    amount,
                    authorization),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowCaptureError(),
            ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference authorization,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CaptureBoundCommissionAsync(
                Proto.BoundCommissionCaptureRequest.Create(
                    reference,
                    payerId,
                    payeeId,
                    gross,
                    authorization,
                    commissionBindingId,
                    externalReference),
                cancellationToken: ct)).ToEscrowDeposit(),
            error => error.ToEscrowCaptureError(),
            ct);

    public Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseAsync(
        Guid operationId,
        PaymentOperationReference reference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Transfer>, EscrowReleaseOperationError>(
            async () =>
            {
                var response = await client.ReleaseAsync(
                    Proto.ReleaseEscrowRequest.Create(operationId, reference),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Transfer?.OperationId)
                    ? null
                    : new Transfer(Guid.Parse(response.Transfer.OperationId));
            },
            error => error.ToEscrowReleaseOperationError(),
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundAsync(
        Guid operationId,
        PaymentOperationReference reference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Refund>, EscrowRefundError>(
            async () =>
            {
                var response = await client.RefundAsync(
                    Proto.RefundEscrowRequest.Create(operationId, reference),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.Id)
                    ? null
                    : new Refund(Guid.Parse(response.Refund.Id));
            },
            error => error.ToEscrowRefundError(),
            ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionAsync(
        PaymentOperationReference reference,
        Money gross,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Refund>, EscrowRefundError>(
            async () =>
            {
                var response = await client.RefundBoundCommissionAsync(
                    Proto.BoundCommissionRefundRequest.Create(reference, gross),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.Refund?.Id)
                    ? null
                    : new Refund(Guid.Parse(response.Refund.Id));
            },
            error => error.ToEscrowRefundError(),
            ct);
}
