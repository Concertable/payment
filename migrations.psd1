@{
    Environment = @{
        ConnectionStrings__PaymentDb = 'Host=localhost;Database=concertable-payment;Username=postgres;Password=postgres'
    }
    Migrations = @(
        @{ Context = 'PaymentDbContext'; Project = 'api/src/Concertable.Payment.Infrastructure'; StartupProject = 'api/src/Concertable.Payment.Web'; OutputDir = 'Data/Migrations' }
    )
}
