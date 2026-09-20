using Aspire.Hosting;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;

public static class AppHost
{
    private const string AuthImage = "ghcr.io/concertable/auth";
    private const string AuthDigest = "sha256:cbd7c429da9d9dd2cc674177760690c53d1414e8057e368eefc3631dfcb62be6";
    private const string AuthMigrationsImage = "ghcr.io/concertable/auth-migrations";
    private const string AuthMigrationsDigest = "sha256:090b1bb80dc7b708508a03883cdfb8e8805b36918589e6d14f2f350cc61c5dcb";

    public static IDistributedApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = StrictDistributedApplication.CreateBuilder(args);
        var postgres = builder.AddPostgresContainer("concertable-payment-postgres-data");
        var paymentDb = postgres.AddDatabase(PaymentConstants.Database);
        var authDb = postgres.AddDatabase(AuthConstants.Database);
        var asb = builder.AddServiceBus();
        asb.Topology().AddPaymentTopology().AddAuthTopology().RunAsEmulator();
        var authMigrations = builder.AddAuthMigrations(AuthMigrationsImage, AuthMigrationsDigest, authDb);
        var auth = builder.AddAuth(AuthImage, AuthDigest, authDb, authMigrations, asb)
                          .WithContainerRuntimeArgs("--user", "root")
                          .WithHttpsEndpoint(targetPort: AuthConstants.ContainerPort, name: "https");
        auth.WithSpaClients([]);
        auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");
        var migrations = builder.AddPaymentMigrations<Projects.Concertable_Payment_Migrations>(paymentDb);
        var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb)
            .WaitForCompletion(migrations);
        builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb)
            .WaitForCompletion(migrations);
        builder.AddStripeCli(paymentWeb);
        return builder;
    }
}
