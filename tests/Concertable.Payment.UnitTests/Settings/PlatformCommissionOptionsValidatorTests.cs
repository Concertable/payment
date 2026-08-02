using System.Globalization;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformCommissionOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(PlatformCommissionOptions options)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{PlatformCommissionOptions.SectionName}:{nameof(PlatformCommissionOptions.ConfigurationId)}"] =
                options.ConfigurationId == Guid.Empty ? null : options.ConfigurationId.ToString(),
            [$"{PlatformCommissionOptions.SectionName}:{nameof(PlatformCommissionOptions.Version)}"] = options.Version,
            [$"{PlatformCommissionOptions.SectionName}:{nameof(PlatformCommissionOptions.Currency)}"] = options.Currency,
            [$"{PlatformCommissionOptions.SectionName}:{nameof(PlatformCommissionOptions.RateBasisPoints)}"] =
                options.RateBasisPoints.ToString(CultureInfo.InvariantCulture)
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new PlatformCommissionOptionsValidator(configuration).Validate(null, options);
    }

    private static PlatformCommissionOptions ValidLaunchOptions() => new()
    {
        ConfigurationId = Guid.NewGuid(),
        Version = "2026-launch",
        Currency = "GBP",
        RateBasisPoints = 1_000
    };

    [Fact]
    public void Validate_ConfigurationIdEmpty_Fails()
    {
        var options = ValidLaunchOptions();
        options.ConfigurationId = Guid.Empty;

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_VersionBlank_Fails(string version)
    {
        var options = ValidLaunchOptions();
        options.Version = version;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_CurrencyNotGbp_Fails()
    {
        var options = ValidLaunchOptions();
        options.Currency = "USD";

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Validate_RateBasisPointsOutOfRange_Fails(int rateBasisPoints)
    {
        var options = ValidLaunchOptions();
        options.RateBasisPoints = rateBasisPoints;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_ValidLaunchConfig_Succeeds()
    {
        Assert.True(Validate(ValidLaunchOptions()).Succeeded);
    }
}
