using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.UnitTests.Infrastructure;

internal static class TestPaymentDbContext
{
    public static PaymentDbContext Unopened() =>
        new(
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseSqlServer("Server=unit-tests;Database=unused;")
                .Options,
            new PaymentConfigurationProvider());
}
