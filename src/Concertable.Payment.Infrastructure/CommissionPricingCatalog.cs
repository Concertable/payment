using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionPricingCatalog
{
    private readonly IReadOnlyDictionary<Guid, CommissionTerms> configurations;

    public CommissionPricingCatalog(IOptions<PlatformCommissionOptions> options)
    {
        configurations = options.Value.Configurations.ToDictionary(
            configuration => configuration.Id,
            configuration => new CommissionTerms(
                configuration.Id,
                configuration.Version,
                Enum.Parse<Currency>(configuration.Currency, ignoreCase: true),
                configuration.RateBasisPoints));
        Current = GetRequired(options.Value.CurrentConfigurationId);
    }

    public CommissionTerms Current { get; }

    public CommissionTerms GetRequired(Guid configurationId) =>
        configurations.TryGetValue(configurationId, out var terms)
            ? terms
            : throw new InvalidOperationException($"Commission configuration {configurationId} is not configured.");
}
