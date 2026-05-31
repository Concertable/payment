using B2BSeedState = Concertable.B2B.Seed.Infrastructure.SeedState;
using CustomerSeedState = Concertable.Customer.Seed.Infrastructure.SeedState;
using Concertable.DataAccess;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Seed.Shared;
using Concertable.Seed.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data.Seeders;

internal class PaymentTestSeeder : ITestSeeder
{
    public int Order => 5;

    private readonly PaymentDbContext context;
    private readonly B2BSeedState b2bSeedState;
    private readonly CustomerSeedState customerSeedState;

    public PaymentTestSeeder(PaymentDbContext context, B2BSeedState b2bSeedState, CustomerSeedState customerSeedState)
    {
        this.context = context;
        this.b2bSeedState = b2bSeedState;
        this.customerSeedState = customerSeedState;
    }

    public Task MigrateAsync(CancellationToken ct = default) => context.Database.MigrateAsync(ct);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await context.PayoutAccounts.SeedIfEmptyAsync(async () =>
        {
            var venueManager1 = PayoutAccountEntity.Create(b2bSeedState.VenueManager1.Id, b2bSeedState.VenueManager1.Email);
            venueManager1.LinkAccount("acct_test_venue1");
            venueManager1.LinkCustomer("cus_test_venue1");
            venueManager1.MarkVerified();

            var venueManager2 = PayoutAccountEntity.Create(b2bSeedState.VenueManager2.Id, b2bSeedState.VenueManager2.Email);
            venueManager2.LinkAccount("acct_test_venue2");
            venueManager2.LinkCustomer("cus_test_venue2");
            venueManager2.MarkVerified();

            var artistManager = PayoutAccountEntity.Create(b2bSeedState.ArtistManager1.Id, b2bSeedState.ArtistManager1.Email);
            artistManager.LinkAccount("acct_test_artist1");
            artistManager.LinkCustomer("cus_test_artist1");
            artistManager.MarkVerified();

            var artistManagerNoArtist = PayoutAccountEntity.Create(b2bSeedState.ArtistManagerNoArtist.Id, b2bSeedState.ArtistManagerNoArtist.Email);
            artistManagerNoArtist.LinkAccount("acct_test_artist2");
            artistManagerNoArtist.LinkCustomer("cus_test_artist2");
            artistManagerNoArtist.MarkVerified();

            var customer = PayoutAccountEntity.Create(customerSeedState.Customer1.Id, customerSeedState.Customer1.Email);
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
