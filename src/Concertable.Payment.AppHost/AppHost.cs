using Aspire.Hosting;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;

public static class AppHost
{
    private const string AuthImage = "ghcr.io/concertable/auth";
    private const string AuthDigest = "sha256:06a295ad6fa01a223000682b0f6efbfba2d5436a8fb2ffaa2d2399526ff3ae69";

    public static IDistributedApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = StrictDistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServerContainer("concertable-payment-sql-data");
        var authDb = sql.AddDatabase(AuthConstants.Database);
        var paymentDb = sql.AddDatabase(PaymentConstants.Database);
        var asb = builder.AddServiceBus();
        asb.Topology().AddPaymentTopology().AddAuthTopology().RunAsEmulator();
        var auth = builder.AddAuth(AuthImage, AuthDigest, authDb, asb)
                          .WithContainerRuntimeArgs("--user", "root")
                          .WithHttpsEndpoint(targetPort: AuthConstants.ContainerPort, name: "https");
        auth.WithSpaClients([]);
        auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");
        var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb);
        builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb);
        builder.AddStripeCli(paymentWeb);
        return builder;
    }
}
