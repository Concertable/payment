using Concertable.DataAccess;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Seeding;
using Concertable.Seeding.Extensions;
using Concertable.Seeding.Factories;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data.Seeders;

internal class PaymentDevSeeder : IDevSeeder
{
    public int Order => 5;

    private readonly PaymentDbContext context;
    private readonly SeedData seedData;
    private readonly TimeProvider timeProvider;

    public PaymentDevSeeder(PaymentDbContext context, SeedData seedData, TimeProvider timeProvider)
    {
        this.context = context;
        this.seedData = seedData;
        this.timeProvider = timeProvider;
    }

    public Task MigrateAsync(CancellationToken ct = default) => context.Database.MigrateAsync(ct);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await context.PayoutAccounts.SeedIfEmptyAsync(async () =>
        {
            seedData.CustomerStripeCustomerId = "cus_UIIy9Gbwfr3uAP";
            var firstCustomer = PayoutAccountEntity.Create(seedData.CustomerIds[0], seedData.CustomerEmails[0]);
            firstCustomer.LinkCustomer(seedData.CustomerStripeCustomerId);
            context.PayoutAccounts.Add(firstCustomer);

            seedData.ArtistManager1StripeAccountId = "acct_1TJiMePysoXmht10";
            seedData.ArtistManager1StripeCustomerId = "cus_UIIy5mCilBtJbR";
            var artistManager1 = PayoutAccountEntity.Create(seedData.ArtistManagerIds[0], seedData.ArtistManagerEmails[0]);
            artistManager1.LinkAccount(seedData.ArtistManager1StripeAccountId);
            artistManager1.LinkCustomer(seedData.ArtistManager1StripeCustomerId);
            artistManager1.MarkVerified();
            context.PayoutAccounts.Add(artistManager1);

            var artistManager2 = PayoutAccountEntity.Create(seedData.ArtistManagerIds[1], seedData.ArtistManagerEmails[1]);
            artistManager2.LinkAccount("acct_1TJiMoPupFslP2qz");
            artistManager2.LinkCustomer("cus_UIIy5415r69RmJ");
            artistManager2.MarkVerified();
            context.PayoutAccounts.Add(artistManager2);

            for (int i = 2; i < seedData.ArtistManagerIds.Count; i++)
            {
                var managerId = seedData.ArtistManagerIds[i];
                var account = PayoutAccountEntity.Create(managerId, seedData.ArtistManagerEmails[i]);
                account.LinkAccount($"acct_dev_artist_{i + 1}");
                account.LinkCustomer($"cus_dev_artist_{i + 1}");
                account.MarkVerified();
                context.PayoutAccounts.Add(account);
            }

            seedData.VenueManager1StripeAccountId = "acct_1TJiMjLxk4aCq1Ui";
            seedData.VenueManager1StripeCustomerId = "cus_UIIymKfHijbNVO";
            var venueManager1 = PayoutAccountEntity.Create(seedData.VenueManagerIds[0], seedData.VenueManagerEmails[0]);
            venueManager1.LinkAccount(seedData.VenueManager1StripeAccountId);
            venueManager1.LinkCustomer(seedData.VenueManager1StripeCustomerId);
            venueManager1.MarkVerified();
            context.PayoutAccounts.Add(venueManager1);

            var venueManager2 = PayoutAccountEntity.Create(seedData.VenueManagerIds[1], seedData.VenueManagerEmails[1]);
            venueManager2.LinkAccount("acct_1TJiPJLLwGSDilbV");
            venueManager2.LinkCustomer("cus_UIJ1qfgxYu624Q");
            venueManager2.MarkVerified();
            context.PayoutAccounts.Add(venueManager2);

            for (int i = 2; i < seedData.VenueManagerIds.Count; i++)
            {
                var managerId = seedData.VenueManagerIds[i];
                var account = PayoutAccountEntity.Create(managerId, seedData.VenueManagerEmails[i]);
                account.LinkAccount($"acct_dev_venue_{i + 1}");
                account.LinkCustomer($"cus_dev_venue_{i + 1}");
                account.MarkVerified();
                context.PayoutAccounts.Add(account);
            }

            await context.SaveChangesAsync(ct);
        });

        await context.Transactions.SeedIfEmptyAsync(async () =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var customerIds = seedData.CustomerIds;
            var artistManagerIds = seedData.ArtistManagerIds;
            var venueManagerIds = seedData.VenueManagerIds;

            var settlementTransactions = new[]
            {
                TransactionFactory.Settlement(venueManagerIds[0], artistManagerIds[0], Guid.NewGuid().ToString(), 15000, TransactionStatus.Complete, 1),
                TransactionFactory.Settlement(venueManagerIds[1], artistManagerIds[1], Guid.NewGuid().ToString(), 20000, TransactionStatus.Complete, 2),
                TransactionFactory.Settlement(venueManagerIds[2], artistManagerIds[2], Guid.NewGuid().ToString(), 18000, TransactionStatus.Complete, 3),
                TransactionFactory.Settlement(venueManagerIds[3], artistManagerIds[3], Guid.NewGuid().ToString(), 17500, TransactionStatus.Complete, 4),
                TransactionFactory.Settlement(venueManagerIds[4], artistManagerIds[4], Guid.NewGuid().ToString(), 16000, TransactionStatus.Complete, 5),
            };
            settlementTransactions[0].CreatedAt = now.AddDays(-58);
            settlementTransactions[1].CreatedAt = now.AddDays(-55);
            settlementTransactions[2].CreatedAt = now.AddDays(-52);
            settlementTransactions[3].CreatedAt = now.AddDays(-49);
            settlementTransactions[4].CreatedAt = now.AddDays(-46);

            var ticketTransactions = new[]
            {
                TransactionFactory.Ticket(customerIds[0], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[1], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[2], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[3], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[4], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[5], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(artistManagerIds[0], venueManagerIds[0], Guid.NewGuid().ToString(), 1500, TransactionStatus.Complete, 1),
                TransactionFactory.Ticket(customerIds[1], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(customerIds[2], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(customerIds[3], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(customerIds[4], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(customerIds[5], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(artistManagerIds[0], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(artistManagerIds[1], venueManagerIds[1], Guid.NewGuid().ToString(), 1200, TransactionStatus.Complete, 2),
                TransactionFactory.Ticket(customerIds[2], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(customerIds[3], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(customerIds[4], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(customerIds[5], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(artistManagerIds[0], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(artistManagerIds[1], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
                TransactionFactory.Ticket(artistManagerIds[2], venueManagerIds[2], Guid.NewGuid().ToString(), 1800, TransactionStatus.Complete, 3),
            };

            context.SettlementTransactions.AddRange(settlementTransactions);
            context.TicketTransactions.AddRange(ticketTransactions);
            await context.SaveChangesAsync(ct);
        });
    }
}
