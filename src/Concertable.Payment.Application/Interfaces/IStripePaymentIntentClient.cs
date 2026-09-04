using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Reunion;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<PaymentOutcome, ChargeError>> ChargeAsync(
        StripeChargeOptions options,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        StripeHoldOptions options,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentError>> GetAsync(
        string paymentIntentId,
        CancellationToken ct = default);
}
