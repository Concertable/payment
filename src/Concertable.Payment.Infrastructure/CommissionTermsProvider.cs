using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionTermsProvider
{
    private readonly IReadOnlyDictionary<Guid, CommissionTerms> termsByConfigurationId;

    public CommissionTermsProvider(IOptions<PlatformCommissionOptions> options)
    {
        termsByConfigurationId = options.Value.Configurations.ToDictionary(
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
        termsByConfigurationId.TryGetValue(configurationId, out var terms)
            ? terms
            : throw new InvalidOperationException($"Commission configuration {configurationId} is not configured.");
}
