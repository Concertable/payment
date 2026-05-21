using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Infrastructure.Services;

internal class FakeStripeTransferClient : IStripeTransferClient
{
    public Task<Result<TransferResponse>> ReleaseAsync(StripeReleaseOptions options) =>
        Task.FromResult(Result.Ok(new TransferResponse("tr_fake")));

    public Task<Result<RefundResponse>> RefundAsync(StripeRefundOptions options) =>
        Task.FromResult(Result.Ok(new RefundResponse("re_fake")));
}
