using Concertable.Payment.Application.Requests;
using Reunion;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeTransferClient
{
    Task<Result<ProviderTransfer, PaymentError>> ReleaseAsync(
        StripeReleaseOptions options,
        CancellationToken ct = default);

    Task<Result<ProviderRefund, PaymentError>> RefundAsync(
        StripeRefundOptions options,
        CancellationToken ct = default);
}
