using Concertable.DataAccess;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Seed.Identity;
using Concertable.Seed.Shared;
using Concertable.Seed.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data.Seeders;

internal class PaymentTestSeeder : ITestSeeder
{
    public int Order => 5;

    private readonly PaymentDbContext context;

    public PaymentTestSeeder(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task MigrateAsync(CancellationToken ct = default) => context.Database.MigrateAsync(ct);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await context.PayoutAccounts.SeedIfEmptyAsync(async () =>
        {
            var venueManager1 = PayoutAccountEntity.Create(SeedUsers.VenueManagerId(1), SeedUsers.VenueManagerEmail(1));
            venueManager1.LinkAccount("acct_test_venue1");
            venueManager1.LinkCustomer("cus_test_venue1");
            venueManager1.MarkVerified();

            var venueManager2 = PayoutAccountEntity.Create(SeedUsers.VenueManagerId(2), SeedUsers.VenueManagerEmail(2));
            venueManager2.LinkAccount("acct_test_venue2");
            venueManager2.LinkCustomer("cus_test_venue2");
            venueManager2.MarkVerified();

            var artistManager = PayoutAccountEntity.Create(SeedUsers.ArtistManagerId(1), SeedUsers.ArtistManagerEmail(1));
            artistManager.LinkAccount("acct_test_artist1");
            artistManager.LinkCustomer("cus_test_artist1");
            artistManager.MarkVerified();

            var artistManagerNoArtist = PayoutAccountEntity.Create(
                SeedUsers.ArtistManagerId(SeedUsers.ManagerCount),
                SeedUsers.ArtistManagerEmail(SeedUsers.ManagerCount));
            artistManagerNoArtist.LinkAccount("acct_test_artist2");
            artistManagerNoArtist.LinkCustomer("cus_test_artist2");
            artistManagerNoArtist.MarkVerified();

            context.PayoutAccounts.AddRange(
                venueManager1,
                venueManager2,
                artistManager,
                artistManagerNoArtist);

            await context.SaveChangesAsync(ct);
        });
    }
}
