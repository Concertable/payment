namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionTaxOptions
{
    public const string SectionName = "PlatformCommissionTax";

    public decimal VatRatePercentage { get; set; }
}
