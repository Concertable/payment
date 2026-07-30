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

        if (string.IsNullOrWhiteSpace(options.Version))
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Version)} must be configured.");

        if (!string.Equals(options.Currency, "GBP", StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Currency)} must be GBP.");

        if (options.RateBasisPoints is < 1 or > 10_000)
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.RateBasisPoints)} must be between 1 and 10,000.");

        return ValidateOptionsResult.Success;
    }
}
