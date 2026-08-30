using Aspire.Hosting;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;

public static class PaymentAppHost
{
    public static IDistributedApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = StrictDistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServerContainer("concertable-payment-sql-data");
        var authDb = sql.AddDatabase(AuthConstants.Database);
        var paymentDb = sql.AddDatabase(PaymentConstants.Database);
        var asb = builder.AddServiceBus();
        asb.Topology().AddPaymentTopology().AddAuthTopology().RunAsEmulator();
        var auth = builder.AddAuth<Projects.Concertable_Auth>(authDb, asb);
        auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");
        var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb);
        builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb);
        builder.AddStripeCli(paymentWeb);
        return builder;
    }
}
