using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionOptionsValidator : IValidateOptions<PlatformCommissionOptions>
{
    private readonly IConfiguration configuration;

    public PlatformCommissionOptionsValidator(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, PlatformCommissionOptions options)
    {
        var prefix = PlatformCommissionOptions.SectionName;
        if (string.IsNullOrWhiteSpace(configuration[$"{prefix}:{nameof(options.ConfigurationId)}"]) ||
            options.ConfigurationId == Guid.Empty)
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.ConfigurationId)} must be a non-empty Guid.");

        if (options.RatePercentage is <= 0m or > 100m)
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.RatePercentage)} must be greater than 0 and no more than 100.");

        if (decimal.Round(options.RatePercentage, 4) != options.RatePercentage)
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.RatePercentage)} cannot have more than four decimal places.");

        return ValidateOptionsResult.Success;
    }
}
