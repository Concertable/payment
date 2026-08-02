using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure.Settings;

internal sealed class PlatformCommissionTaxOptionsValidator : IValidateOptions<PlatformCommissionTaxOptions>
{
    private readonly IConfiguration configuration;

    public PlatformCommissionTaxOptionsValidator(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, PlatformCommissionTaxOptions options)
    {
        var key = $"{PlatformCommissionTaxOptions.SectionName}:{nameof(options.VatRatePercentage)}";
        if (configuration[key] is null)
            return ValidateOptionsResult.Fail($"{key} must be configured.");

        if (options.VatRatePercentage is < 0m or > 100m)
            return ValidateOptionsResult.Fail($"{key} must be between 0 and 100.");

        return decimal.Round(options.VatRatePercentage, 4) != options.VatRatePercentage
            ? ValidateOptionsResult.Fail($"{key} cannot have more than four decimal places.")
            : ValidateOptionsResult.Success;
    }
}
