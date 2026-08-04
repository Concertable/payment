using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeTransferClient
{
    Task<Result<Transfer, ReleaseError>> ReleaseAsync(StripeReleaseOptions options);
    Task<Result<Refund, RefundError>> RefundAsync(StripeRefundOptions options);
}
