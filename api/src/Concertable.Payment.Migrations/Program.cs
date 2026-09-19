using Concertable.Payment.Migrations;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PaymentDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings__PaymentDb' is required for the Payment migration job.");

await PaymentMigrationJob.RunAsync(connectionString).ConfigureAwait(false);
