using Concertable.Payment.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddWorkerHost();

var app = builder.Build();

await app.MigrateStoresAsync();

app.Run();
