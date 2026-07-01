using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeTransferClient : IStripeTransferClient
{
    public Task<Result<Transfer>> ReleaseAsync(StripeReleaseOptions options) =>
        Task.FromResult(Result.Ok(new Transfer("tr_fake")));

    public Task<Result<Refund>> RefundAsync(StripeRefundOptions options) =>
        Task.FromResult(Result.Ok(new Refund("re_fake")));
}
