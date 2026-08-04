using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClient
{
    Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(StripeChargeOptions options);
    Task<Result<PaymentOutcome, PaymentError>> HoldAsync(StripeHoldOptions options);
}
