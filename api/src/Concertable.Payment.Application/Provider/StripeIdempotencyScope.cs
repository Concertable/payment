namespace Concertable.Payment.Application.Provider;

internal enum StripeIdempotencyScope
{
    PaymentSession = 0,
    FinancialOperation = 1,
    CommissionBinding = 2
}
