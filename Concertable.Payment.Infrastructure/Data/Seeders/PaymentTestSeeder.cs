using B2BSeedData = Concertable.B2B.Seed.Infrastructure.SeedData;
using CustomerSeedData = Concertable.Customer.Seed.SeedData;
using Concertable.DataAccess;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Seed;
using Concertable.Seed.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data.Seeders;

internal class PaymentTestSeeder : ITestSeeder
{
    public int Order => 5;

    private readonly PaymentDbContext context;
    private readonly B2BSeedData b2bSeedData;
    private readonly CustomerSeedData customerSeedData;

    public PaymentTestSeeder(PaymentDbContext context, B2BSeedData b2bSeedData, CustomerSeedData customerSeedData)
    {
        this.context = context;
        this.b2bSeedData = b2bSeedData;
        this.customerSeedData = customerSeedData;
    }

    public Task MigrateAsync(CancellationToken ct = default) => context.Database.MigrateAsync(ct);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await context.PayoutAccounts.SeedIfEmptyAsync(async () =>
        {
            var venueManager1 = PayoutAccountEntity.Create(b2bSeedData.VenueManager1.Id, b2bSeedData.VenueManager1.Email);
            venueManager1.LinkAccount("acct_test_venue1");
            venueManager1.LinkCustomer("cus_test_venue1");
            venueManager1.MarkVerified();

            var venueManager2 = PayoutAccountEntity.Create(b2bSeedData.VenueManager2.Id, b2bSeedData.VenueManager2.Email);
            venueManager2.LinkAccount("acct_test_venue2");
            venueManager2.LinkCustomer("cus_test_venue2");
            venueManager2.MarkVerified();

            var artistManager = PayoutAccountEntity.Create(b2bSeedData.ArtistManager1.Id, b2bSeedData.ArtistManager1.Email);
            artistManager.LinkAccount("acct_test_artist1");
            artistManager.LinkCustomer("cus_test_artist1");
            artistManager.MarkVerified();

            var artistManagerNoArtist = PayoutAccountEntity.Create(b2bSeedData.ArtistManagerNoArtist.Id, b2bSeedData.ArtistManagerNoArtist.Email);
            artistManagerNoArtist.LinkAccount("acct_test_artist2");
            artistManagerNoArtist.LinkCustomer("cus_test_artist2");
            artistManagerNoArtist.MarkVerified();

            var customer = PayoutAccountEntity.Create(customerSeedData.Customer.Id, customerSeedData.Customer.Email);
            customer.LinkCustomer("cus_test_customer");

            context.PayoutAccounts.AddRange(
                venueManager1,
                venueManager2,
                artistManager,
                artistManagerNoArtist,
                customer);

            await context.SaveChangesAsync(ct);
        });
    }
}
