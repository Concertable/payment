namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionOptions
{
    public const string SectionName = "PlatformCommission";

    public Guid CurrentConfigurationId { get; set; }
    public PlatformCommissionRevisionOptions[] Configurations { get; set; } = [];
}

internal sealed class PlatformCommissionRevisionOptions
{
    public Guid Id { get; set; }
    public string Version { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public int RateBasisPoints { get; set; }
}
