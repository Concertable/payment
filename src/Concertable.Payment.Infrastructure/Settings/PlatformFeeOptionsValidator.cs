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
        var configured = configuration[$"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)}"];
        if (string.IsNullOrWhiteSpace(configured))
            return ValidateOptionsResult.Fail($"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)} must be configured.");

        if (options.Fee < 0)
            return ValidateOptionsResult.Fail($"{PlatformFeeOptions.SectionName}:{nameof(PlatformFeeOptions.Fee)} cannot be negative.");

        return ValidateOptionsResult.Success;
    }
}
