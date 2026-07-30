using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Concertable.Payment.Infrastructure.Data;

internal sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(DesignTimeConfiguration.ConnectionString())
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
