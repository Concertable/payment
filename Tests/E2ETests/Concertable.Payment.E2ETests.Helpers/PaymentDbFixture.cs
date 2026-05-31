using System.Data.Common;
using Aspire.Hosting;
using Respawn;
using Respawn.Graph;

namespace Concertable.E2ETests;

/// <summary>
/// Respawns the Payment DB between scenarios. <c>PayoutAccounts</c> are excluded from resets — they are
/// provisioned once at startup via <c>CredentialRegisteredEvent</c>
/// and must persist for the full test run.
/// <para><see cref="Table"/> constructor is <c>Table(schema, name)</c> — schema first, name second.</para>
/// </summary>
public sealed class PaymentDbFixture
{
    private readonly RespawnableDb db = new();
    public PaymentDb Payment { get; private set; } = null!;
    public DbConnection Connection => db.Connection;
    public bool IsInitialized => db.IsInitialized;

    public async Task InitializeAsync(DistributedApplication app)
    {
        await db.InitializeAsync(app, AppHostConstants.Databases.Payment, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory", new Table("payment", "PayoutAccounts")],
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true
        });
        Payment = new PaymentDb(db.Connection);
    }

    public Task ResetAsync() => db.ResetAsync();
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
