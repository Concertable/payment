using Concertable.Payment.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddWebHost();

var app = builder.Build();

app.UseWebHost();

app.Run();

public sealed partial class Program
{ }
