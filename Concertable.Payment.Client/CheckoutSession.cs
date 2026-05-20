namespace Concertable.Payment.Client;

public record CheckoutSession(string ClientSecret, string CustomerSession, string CustomerId);
