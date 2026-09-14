namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformFeeOptions
{
    public const string SectionName = "PlatformFee";

    public decimal Fee { get; set; }
}
