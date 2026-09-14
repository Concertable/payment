@{
    Environment = @{
        ConnectionStrings__PaymentDb = 'Server=localhost;Database=concertable-payment;Trusted_Connection=True;TrustServerCertificate=True'
    }
    Migrations = @(
        @{ Context = 'PaymentDbContext'; Project = 'api/src/Concertable.Payment.Infrastructure'; StartupProject = 'api/src/Concertable.Payment.Web'; OutputDir = 'Data/Migrations' }
    )
}
