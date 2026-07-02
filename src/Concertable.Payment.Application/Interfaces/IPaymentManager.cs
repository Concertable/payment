using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentManager
{
    Task<Result<PaymentOutcome>> ChargeAsync(ChargeRequest request, CancellationToken ct = default);
    Task<Result<PaymentOutcome>> HoldAsync(HoldRequest request, CancellationToken ct = default);
    Task<Result<Transfer>> ReleaseAsync(ReleaseRequest request, CancellationToken ct = default);
    Task<Result<Refund>> RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<Result> CaptureAsync(CaptureRequest request, CancellationToken ct = default);
}
