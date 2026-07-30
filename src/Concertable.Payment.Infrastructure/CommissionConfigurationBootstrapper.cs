using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionConfigurationBootstrapper
{
    private readonly PaymentDbContext context;
    private readonly PlatformCommissionOptions options;
    private readonly TimeProvider timeProvider;

    public CommissionConfigurationBootstrapper(
        PaymentDbContext context,
        IOptions<PlatformCommissionOptions> options,
        TimeProvider timeProvider)
    {
        this.context = context;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    public async Task EnsureConfiguredRevisionAsync(CancellationToken ct = default)
    {
        var configured = GetConfiguredTerms();
        var existing = await FindExistingAsync(configured.Id, configured.Version, ct);
        if (existing.Count > 0)
        {
            ValidateExisting(existing, configured);
            return;
        }

        context.CommissionConfigurations.Add(
            CommissionConfigurationEntity.Create(
                configured.Id,
                configured.Version,
                configured.Currency,
                configured.RateBasisPoints,
                timeProvider.GetUtcNow()));

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            existing = await FindExistingAsync(configured.Id, configured.Version, ct);
            if (existing.Count == 0)
                throw;

            ValidateExisting(existing, configured);
        }
    }

    private async Task<List<CommissionConfigurationEntity>> FindExistingAsync(
        Guid id,
        string version,
        CancellationToken ct) =>
        await context.CommissionConfigurations
            .AsNoTracking()
            .Where(c => c.Id == id || c.Version == version)
            .ToListAsync(ct);

    private (Guid Id, string Version, Currency Currency, int RateBasisPoints) GetConfiguredTerms() =>
        (
            options.ConfigurationId,
            options.Version,
            Enum.Parse<Currency>(options.Currency, ignoreCase: true),
            options.RateBasisPoints
        );

    private static void ValidateExisting(
        IReadOnlyCollection<CommissionConfigurationEntity> existing,
        (Guid Id, string Version, Currency Currency, int RateBasisPoints) configured)
    {
        if (existing.Count == 1 &&
            existing.Single().HasTerms(
                configured.Id,
                configured.Version,
                configured.Currency,
                configured.RateBasisPoints))
            return;

        throw new InvalidOperationException(
            "The configured commission revision id or version already identifies different immutable terms.");
    }
}
