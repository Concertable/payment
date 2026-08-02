using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformCommissionOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(PlatformCommissionOptions options) =>
        new PlatformCommissionOptionsValidator().Validate(null, options);

    private static PlatformCommissionOptions ValidLaunchOptions()
    {
        var id = Guid.NewGuid();
        return new PlatformCommissionOptions
        {
            CurrentConfigurationId = id,
            Configurations =
            [
                new PlatformCommissionRevisionOptions
                {
                    Id = id,
                    Version = "2026-launch",
                    Currency = "GBP",
                    RateBasisPoints = 1_000
                }
            ]
        };
    }

    [Fact]
    public void Validate_CurrentConfigurationIdEmpty_Fails()
    {
        var options = ValidLaunchOptions();
        options.CurrentConfigurationId = Guid.Empty;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_CurrentConfigurationMissingFromCatalog_Fails()
    {
        var options = ValidLaunchOptions();
        options.CurrentConfigurationId = Guid.NewGuid();

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_NoConfigurations_Fails()
    {
        var options = ValidLaunchOptions();
        options.Configurations = [];

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_DuplicateId_Fails()
    {
        var options = ValidLaunchOptions();
        options.Configurations = [options.Configurations[0], options.Configurations[0]];

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_DuplicateVersion_Fails()
    {
        var options = ValidLaunchOptions();
        options.Configurations =
        [
            options.Configurations[0],
            new PlatformCommissionRevisionOptions
            {
                Id = Guid.NewGuid(),
                Version = options.Configurations[0].Version,
                Currency = "GBP",
                RateBasisPoints = 500
            }
        ];

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_VersionBlank_Fails(string version)
    {
        var options = ValidLaunchOptions();
        options.Configurations[0].Version = version;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_CurrencyNotGbp_Fails()
    {
        var options = ValidLaunchOptions();
        options.Configurations[0].Currency = "USD";

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Validate_RateBasisPointsOutOfRange_Fails(int rateBasisPoints)
    {
        var options = ValidLaunchOptions();
        options.Configurations[0].RateBasisPoints = rateBasisPoints;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void Validate_ValidLaunchConfig_Succeeds()
    {
        Assert.True(Validate(ValidLaunchOptions()).Succeeded);
    }
}
