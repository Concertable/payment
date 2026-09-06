using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Reunion;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<ProviderPaymentOutcome, ChargeError>> ChargeAsync(
        StripeChargeOptions options,
        CancellationToken ct = default);

    Task<Result<ProviderPaymentOutcome, PaymentError>> HoldAsync(
        StripeHoldOptions options,
        CancellationToken ct = default);

    Task<Result<ProviderPaymentOutcome, PaymentError>> GetAsync(
        string paymentIntentId,
        CancellationToken ct = default);
}
