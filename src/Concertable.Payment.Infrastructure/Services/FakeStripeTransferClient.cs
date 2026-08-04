using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeTransferClient : IStripeTransferClient
{
    public Task<Result<Transfer, ReleaseError>> ReleaseAsync(StripeReleaseOptions options) =>
        Task.FromResult(Result.Success<Transfer, ReleaseError>(new Transfer("tr_fake")));

    public Task<Result<Refund, RefundError>> RefundAsync(StripeRefundOptions options) =>
        Task.FromResult(Result.Success<Refund, RefundError>(new Refund("re_fake")));
}
