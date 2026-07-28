using System.Globalization;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Settings;

public sealed class PlatformFeeOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(string? feeValue)
    {
        var settings = new Dictionary<string, string?>();
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
    public void Validate_FeeMissing_Fails()
    {
        var result = Validate(feeValue: null);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    public void Validate_FeeConfigured_Succeeds(string feeValue)
    {
        var result = Validate(feeValue);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NegativeFee_Fails()
    {
        var result = Validate(feeValue: "-1");

        Assert.True(result.Failed);
    }
}
