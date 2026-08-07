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
            [$"{PlatformCommissionOptions.SectionName}:{nameof(PlatformCommissionOptions.RatePercentage)}"] =
                options.RatePercentage.ToString(CultureInfo.InvariantCulture)
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new PlatformCommissionOptionsValidator(configuration).Validate(null, options);
    }

    private static PlatformCommissionOptions ValidLaunchOptions() => new()
    {
        ConfigurationId = Guid.NewGuid(),
        RatePercentage = 10m
    };

    [Fact]
    public void Validate_ConfigurationIdEmpty_Fails()
    {
        var options = ValidLaunchOptions();
        options.ConfigurationId = Guid.Empty;

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.01)]
    public void Validate_RatePercentageOutOfRange_Fails(decimal ratePercentage)
    {
        var options = ValidLaunchOptions();
        options.RatePercentage = ratePercentage;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_UnsupportedPrecision_Fails()
    {
        var options = ValidLaunchOptions();
        options.RatePercentage = 5.00001m;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_ValidLaunchConfig_Succeeds()
    {
        Assert.True(Validate(ValidLaunchOptions()).Succeeded);
    }
}
