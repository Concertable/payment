using System.Globalization;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformFeeOptionsValidatorTests
{
    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(bool useRealStripe, string? feeValue)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ExternalServices:UseRealStripe"] = useRealStripe.ToString()
        };
        if (feeValue is not null)
            settings[$"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)}"] = feeValue;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new PlatformFeeOptions
        {
            Fee = feeValue is null ? 0m : decimal.Parse(feeValue, CultureInfo.InvariantCulture)
        };

        return new PlatformFeeOptionsValidator(configuration).Validate(null, options);
    }

    [Fact]
    public void Validate_RealStripeAndFeeMissing_Fails()
    {
        var result = Validate(useRealStripe: true, feeValue: null);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12.50")]
    public void Validate_RealStripeAndFeeConfigured_Succeeds(string feeValue)
    {
        var result = Validate(useRealStripe: true, feeValue: feeValue);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FakeStripeAndFeeMissing_Succeeds()
    {
        var result = Validate(useRealStripe: false, feeValue: null);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NegativeFee_Fails()
    {
        var result = Validate(useRealStripe: true, feeValue: "-1");

        Assert.True(result.Failed);
    }
}
