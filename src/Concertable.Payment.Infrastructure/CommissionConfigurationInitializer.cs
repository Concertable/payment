using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionConfigurationInitializer
{
    private readonly ICommissionConfigurationRepository configurationRepository;
    private readonly PlatformCommissionOptions options;
    private readonly TimeProvider timeProvider;

    public CommissionConfigurationInitializer(
        ICommissionConfigurationRepository configurationRepository,
        IOptions<PlatformCommissionOptions> options,
        TimeProvider timeProvider)
    {
        this.configurationRepository = configurationRepository;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var currency = Enum.Parse<Currency>(options.Currency, ignoreCase: true);
        var configuration = await configurationRepository.GetOrCreateAsync(
            CommissionConfigurationEntity.Create(
                options.ConfigurationId,
                options.Version,
                currency,
                options.RateBasisPoints,
                timeProvider.GetUtcNow()),
            ct);

        if (configuration.Id != options.ConfigurationId ||
            !configuration.Matches(options.Version, currency, options.RateBasisPoints))
            throw new InvalidOperationException(
                "Configured commission id or version identifies different immutable terms.");
    }
}
