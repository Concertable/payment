var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServerContainer("concertable-payment-sql-data");
var authDb = sql.AddDatabase(AppHostConstants.Databases.Auth);
var b2bDb = sql.AddDatabase(AppHostConstants.Databases.B2B);
var paymentDb = sql.AddDatabase(AppHostConstants.Databases.Payment);

var asb = builder.AddServiceBus();
asb.Topology().AddPaymentTopology();

var auth = builder.AddAuth<Projects.Concertable_Auth>(authDb, b2bDb, asb);
auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");

var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb);
builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb);
builder.AddStripeCli(paymentWeb);

builder.Build().Run();
