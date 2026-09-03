namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class PaymentProviderUnavailableException()
    : Exception("The payment provider is temporarily unavailable.");
