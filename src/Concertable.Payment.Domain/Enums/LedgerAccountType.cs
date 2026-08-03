namespace Concertable.Payment.Domain.Enums;

internal enum LedgerAccountType
{
    PlatformRevenue,
    StripeClearing,
    Payable,
    Receivable,
    VatLiability
}
