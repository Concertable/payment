using Concertable.Payment.Application.Requests;
using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<PaymentResponse>> ChargeAsync(StripeChargeOptions options);
    Task<Result<PaymentResponse>> HoldAsync(StripeHoldOptions options);
}
