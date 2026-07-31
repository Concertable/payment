using System.Globalization;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformCommissionTaxOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(int? vatRateBasisPoints)
    {
        var settings = new Dictionary<string, string?>();
        if (vatRateBasisPoints is not null)
            settings[$"{PlatformCommissionTaxOptions.SectionName}:{nameof(PlatformCommissionTaxOptions.VatRateBasisPoints)}"] =
                vatRateBasisPoints.Value.ToString(CultureInfo.InvariantCulture);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new PlatformCommissionTaxOptions { VatRateBasisPoints = vatRateBasisPoints ?? 0 };

        return new PlatformCommissionTaxOptionsValidator(configuration).Validate(null, options);
    }

    [Fact]
    public void Validate_VatRateBasisPointsMissing_Fails()
    {
        Assert.True(Validate(vatRateBasisPoints: null).Failed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_VatRateBasisPointsOutOfRange_Fails(int vatRateBasisPoints)
    {
        Assert.True(Validate(vatRateBasisPoints).Failed);
    }

    [Fact]
    public void Validate_ZeroVat_Succeeds()
    {
        Assert.True(Validate(vatRateBasisPoints: 0).Succeeded);
    }

    [Fact]
    public void Validate_VatRateBasisPointsConfigured_Succeeds()
    {
        Assert.True(Validate(vatRateBasisPoints: 2_000).Succeeded);
    }
}
