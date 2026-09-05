using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeTransferClient : IStripeTransferClient
{
    public Task<Result<ProviderTransfer, PaymentError>> ReleaseAsync(
        StripeReleaseOptions options,
        CancellationToken ct = default) =>
        Task.FromResult(Result<ProviderTransfer, PaymentError>.Success(new("tr_fake")));

    public Task<Result<ProviderRefund, PaymentError>> RefundAsync(
        StripeRefundOptions options,
        CancellationToken ct = default) =>
        Task.FromResult(Result<ProviderRefund, PaymentError>.Success(new("re_fake")));
}
