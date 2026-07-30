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
        var key = $"{PlatformCommissionTaxOptions.SectionName}:{nameof(options.VatRateBasisPoints)}";
        if (configuration[key] is null)
            return ValidateOptionsResult.Fail($"{key} must be configured.");

        return options.VatRateBasisPoints is < 0 or > 10_000
            ? ValidateOptionsResult.Fail($"{key} must be between 0 and 10,000.")
            : ValidateOptionsResult.Success;
    }
}
