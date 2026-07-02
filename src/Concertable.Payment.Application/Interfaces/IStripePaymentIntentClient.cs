using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<PaymentOutcome>> ChargeAsync(StripeChargeOptions options);
    Task<Result<PaymentOutcome>> HoldAsync(StripeHoldOptions options);
}
