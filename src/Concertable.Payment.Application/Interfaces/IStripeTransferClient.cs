using Concertable.Payment.Application.Requests;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeTransferClient
{
    Task<Result<Transfer, PaymentError>> ReleaseAsync(
        StripeReleaseOptions options,
        CancellationToken ct = default);

    Task<Result<Refund, PaymentError>> RefundAsync(
        StripeRefundOptions options,
        CancellationToken ct = default);
}
