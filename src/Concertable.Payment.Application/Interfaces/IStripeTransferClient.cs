using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeTransferClient
{
    Task<Result<Transfer>> ReleaseAsync(StripeReleaseOptions options);
    Task<Result<Refund>> RefundAsync(StripeRefundOptions options);
}
