using Concertable.Payment.Application.Requests;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(
        StripeChargeOptions options,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        StripeHoldOptions options,
        CancellationToken ct = default);
}
