using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionOptionsValidator : IValidateOptions<PlatformCommissionOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformCommissionOptions options)
    {
        var prefix = PlatformCommissionOptions.SectionName;
        if (options.CurrentConfigurationId == Guid.Empty)
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.CurrentConfigurationId)} must be a non-empty Guid.");

        if (options.Configurations is not { Length: > 0 })
            return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} must contain at least one revision.");

        var ids = new HashSet<Guid>();
        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var configuration in options.Configurations)
        {
            if (configuration.Id == Guid.Empty)
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} contains an empty Id.");
            if (!ids.Add(configuration.Id))
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} contains duplicate Id {configuration.Id}.");
            if (string.IsNullOrWhiteSpace(configuration.Version))
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} contains a blank Version.");
            if (!versions.Add(configuration.Version))
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} contains duplicate Version {configuration.Version}.");
            if (!string.Equals(configuration.Currency, "GBP", StringComparison.OrdinalIgnoreCase))
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} currency must be GBP.");
            if (configuration.RateBasisPoints is < 1 or > 10_000)
                return ValidateOptionsResult.Fail($"{prefix}:{nameof(options.Configurations)} rate must be between 1 and 10,000 basis points.");
        }

        return ids.Contains(options.CurrentConfigurationId)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{prefix}:{nameof(options.CurrentConfigurationId)} must identify a configured revision.");
    }
}
