using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeTransferClient : IStripeTransferClient
{
    public Task<Result<Transfer, PaymentError>> ReleaseAsync(
        StripeReleaseOptions options,
        CancellationToken ct = default) =>
        Task.FromResult(Result<Transfer, PaymentError>.Success(new Transfer("tr_fake")));

    public Task<Result<Refund, PaymentError>> RefundAsync(
        StripeRefundOptions options,
        CancellationToken ct = default) =>
        Task.FromResult(Result<Refund, PaymentError>.Success(new Refund("re_fake")));
}
