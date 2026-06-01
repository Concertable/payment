namespace Concertable.Payment.Infrastructure;

public static class Schema
{
    public const string Name = "payment";

    public static class Tables
    {
        public const string PayoutAccounts = "PayoutAccounts";
        public const string Transactions = "Transactions";
        public const string StripeEvents = "StripeEvents";
        public const string Escrows = "Escrows";
    }
}
