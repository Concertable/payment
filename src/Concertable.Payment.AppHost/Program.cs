using Concertable.Auth.Hosting;
using Concertable.B2B.Hosting;
using Concertable.Payment.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServerContainer("concertable-payment-sql-data");
var authDb = sql.AddDatabase(AuthConstants.Database);
var b2bDb = sql.AddDatabase(B2BConstants.Database);
var paymentDb = sql.AddDatabase(PaymentConstants.Database);

var asb = builder.AddServiceBus();
asb.Topology().AddPaymentTopology().AddAuthTopology().RunAsEmulator();

var auth = builder.AddAuth<Projects.Concertable_Auth>(authDb, b2bDb, asb);
auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");

var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb);
builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb);
builder.AddStripeCli(paymentWeb);

builder.Build().Run();
