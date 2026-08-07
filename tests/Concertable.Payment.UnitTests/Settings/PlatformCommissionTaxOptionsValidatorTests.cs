using System.Globalization;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformCommissionTaxOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(decimal? vatRatePercentage)
    {
        var settings = new Dictionary<string, string?>();
        if (vatRatePercentage is not null)
            settings[$"{PlatformCommissionTaxOptions.SectionName}:{nameof(PlatformCommissionTaxOptions.VatRatePercentage)}"] =
                vatRatePercentage.Value.ToString(CultureInfo.InvariantCulture);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new PlatformCommissionTaxOptions
        {
            VatRatePercentage = vatRatePercentage ?? 0m
        };

        return new PlatformCommissionTaxOptionsValidator(configuration).Validate(null, options);
    }

    [Fact]
    public void Validate_VatRatePercentageMissing_Fails()
    {
        Assert.True(Validate(vatRatePercentage: null).Failed);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Validate_VatRatePercentageOutOfRange_Fails(decimal vatRatePercentage)
    {
        Assert.True(Validate(vatRatePercentage).Failed);
    }

    [Fact]
    public void Validate_UnsupportedPrecision_Fails()
    {
        Assert.True(Validate(vatRatePercentage: 20.00001m).Failed);
    }

    [Fact]
    public void Validate_ZeroVat_Succeeds()
    {
        Assert.True(Validate(vatRatePercentage: 0m).Succeeded);
    }

    [Fact]
    public void Validate_VatRatePercentageConfigured_Succeeds()
    {
        Assert.True(Validate(vatRatePercentage: 20m).Succeeded);
    }
}
