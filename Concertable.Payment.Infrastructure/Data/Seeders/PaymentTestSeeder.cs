using Concertable.DataAccess;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Seeding;
using Concertable.Seeding.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data.Seeders;

internal class PaymentTestSeeder : ITestSeeder
{
    public int Order => 5;

    private readonly PaymentDbContext context;
    private readonly SeedData seedData;

    public PaymentTestSeeder(PaymentDbContext context, SeedData seedData)
    {
        this.context = context;
        this.seedData = seedData;
    }

    public Task MigrateAsync(CancellationToken ct = default) => context.Database.MigrateAsync(ct);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await context.PayoutAccounts.SeedIfEmptyAsync(async () =>
        {
            seedData.VenueManager1StripeAccountId = "acct_test_venue1";
            seedData.VenueManager1StripeCustomerId = "cus_test_venue1";
            seedData.ArtistManager1StripeAccountId = "acct_test_artist1";
            seedData.ArtistManager1StripeCustomerId = "cus_test_artist1";

            var venueManager1 = PayoutAccountEntity.Create(seedData.VenueManager1.Id, seedData.VenueManager1.Email);
            venueManager1.LinkAccount(seedData.VenueManager1StripeAccountId);
            venueManager1.LinkCustomer(seedData.VenueManager1StripeCustomerId);
            venueManager1.MarkVerified();

            var venueManager2 = PayoutAccountEntity.Create(seedData.VenueManager2.Id, seedData.VenueManager2.Email);
            venueManager2.LinkAccount("acct_test_venue2");
            venueManager2.LinkCustomer("cus_test_venue2");
            venueManager2.MarkVerified();

            var artistManager = PayoutAccountEntity.Create(seedData.ArtistManager1.Id, seedData.ArtistManager1.Email);
            artistManager.LinkAccount(seedData.ArtistManager1StripeAccountId);
            artistManager.LinkCustomer(seedData.ArtistManager1StripeCustomerId);
            artistManager.MarkVerified();

            var artistManagerNoArtist = PayoutAccountEntity.Create(seedData.ArtistManagerNoArtist.Id, seedData.ArtistManagerNoArtist.Email);
            artistManagerNoArtist.LinkAccount("acct_test_artist2");
            artistManagerNoArtist.LinkCustomer("cus_test_artist2");
            artistManagerNoArtist.MarkVerified();

            var customer = PayoutAccountEntity.Create(seedData.Customer.Id, seedData.Customer.Email);
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
