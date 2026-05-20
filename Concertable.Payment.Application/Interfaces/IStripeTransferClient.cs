using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeTransferClient
{
    Task<Result<TransferResponse>> ReleaseAsync(StripeReleaseOptions options);
    Task<Result<RefundResponse>> RefundAsync(StripeRefundOptions options);
}
