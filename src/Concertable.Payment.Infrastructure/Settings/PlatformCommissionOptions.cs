namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionOptions
{
    public const string SectionName = "PlatformCommission";

    public Guid ConfigurationId { get; set; }
    public string Version { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public int RateBasisPoints { get; set; }
}
