namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionOptions
{
    public const string SectionName = "PlatformCommission";

    public Guid ConfigurationId { get; set; }
    public decimal RatePercentage { get; set; }
}
