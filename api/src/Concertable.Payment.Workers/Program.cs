using Concertable.Payment.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddWorkerHost();

var app = builder.Build();

app.Run();
