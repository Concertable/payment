namespace Concertable.Payment.Domain.Enums;

public enum LedgerAccountType
{
    PlatformRevenue,
    StripeClearing,
    Payable,
    Receivable,
    VatLiability
}
