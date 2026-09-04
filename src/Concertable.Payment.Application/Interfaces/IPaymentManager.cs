using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Reunion;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentManager
{
    Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentRejection>> SettleAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentRejection>> SettleAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentRejection>> SettleBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid operationId,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> HoldBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> GetPaymentOutcomeAsync(
        string paymentIntentId,
        PaymentSession session,
        CancellationToken ct = default);

    Task<Result<Transfer, PaymentError>> ReleaseAsync(ReleaseRequest request, CancellationToken ct = default);
    Task<Result<Refund, PaymentError>> RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<UnitResult<PaymentError>> CaptureAsync(CaptureRequest request, CancellationToken ct = default);
}
