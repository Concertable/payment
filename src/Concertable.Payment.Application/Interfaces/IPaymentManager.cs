using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentManager
{
    Task<Result<PaymentOutcome>> ChargeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome>> SettleAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<Transfer>> ReleaseAsync(ReleaseRequest request, CancellationToken ct = default);
    Task<Result<Refund>> RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<Result> CaptureAsync(CaptureRequest request, CancellationToken ct = default);
}
