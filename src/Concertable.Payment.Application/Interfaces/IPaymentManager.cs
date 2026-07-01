using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentManager
{
    Task<Result<PaymentResponse>> ChargeAsync(ChargeRequest request, CancellationToken ct = default);
    Task<Result<PaymentResponse>> HoldAsync(HoldRequest request, CancellationToken ct = default);
    Task<Result<TransferResponse>> ReleaseAsync(ReleaseRequest request, CancellationToken ct = default);
    Task<Result<RefundResponse>> RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<Result> CaptureAsync(CaptureRequest request, CancellationToken ct = default);
}
