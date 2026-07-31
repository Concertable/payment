namespace Concertable.Payment.Infrastructure;

internal static class Schema
{
    public const string Name = "payment";

    public static class Tables
    {
        public const string PayoutAccounts = "PayoutAccounts";
        public const string Transactions = "Transactions";
        public const string StripeEvents = "StripeEvents";
        public const string Escrows = "Escrows";
        public const string LedgerAccounts = "LedgerAccounts";
        public const string LedgerTransactions = "LedgerTransactions";
        public const string LedgerEntries = "LedgerEntries";
        public const string CommissionConfigurations = "CommissionConfigurations";
        public const string CommissionAuthorizations = "CommissionAuthorizations";
        public const string CommissionAuthorizationClaims = "CommissionAuthorizationClaims";
        public const string PaymentRefunds = "PaymentRefunds";
    }
}
