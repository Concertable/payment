using Reunion;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowService
{
    Task<Result<PaymentSessionExecution, PaymentOperationError>> AuthorizeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference reference,
        Guid operationId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        PaymentOperationReference reference,
        Guid operationId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentMethodId,
        PaymentSession session,
        PaymentOperationReference reference,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        PaymentOperationReference reference,
        Guid operationId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentIntentId,
        PaymentOperationReference reference,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseByReferenceAsync(
        Guid operationId,
        PaymentOperationReference reference,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundByReferenceAsync(
        PaymentOperationReference reference,
        Money? amount,
        string? reason,
        Guid operationId,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionByReferenceAsync(
        PaymentOperationReference reference,
        Money gross,
        string? reason = null,
        CancellationToken ct = default);

    Task<Option<EscrowDto>> GetByReferenceAsync(
        PaymentOperationReference reference,
        CancellationToken ct = default);
}
