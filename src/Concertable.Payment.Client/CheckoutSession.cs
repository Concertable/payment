namespace Concertable.Payment.Client;

public sealed record CheckoutSession(string ClientSecret, string CustomerSession, string CustomerId);
