using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformFeeOptionsValidator : IValidateOptions<PlatformFeeOptions>
{
    private readonly IConfiguration configuration;

    public PlatformFeeOptionsValidator(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, PlatformFeeOptions options)
    {
        if (options.Fee < 0)
            return ValidateOptionsResult.Fail($"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)} cannot be negative.");

        // Where real Stripe money moves (prod, and E2E's Stripe test mode) the fee must be an explicit
        // decision — refuse a silently-defaulted 0. Checked on the raw config key, not options.Fee, because
        // a non-nullable decimal reads 0 whether the key is absent or explicitly 0.
        var useRealStripe = configuration.GetSection("ExternalServices").GetValue<bool>("UseRealStripe");
        var configured = configuration[$"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)}"];
        if (useRealStripe && string.IsNullOrWhiteSpace(configured))
            return ValidateOptionsResult.Fail(
                $"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)} must be explicitly configured when real Stripe is enabled — refusing to default to 0, because silently taking no platform fee is a financial risk.");

        return ValidateOptionsResult.Success;
    }
}
