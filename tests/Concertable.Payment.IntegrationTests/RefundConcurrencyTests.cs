using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Testing.Integration;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Concertable.Payment.IntegrationTests;

public sealed class RefundConcurrencyTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public RefundConcurrencyTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task ConcurrentEscrowPartialRefunds_OfDifferentAmounts_CannotOverRefundPastPayeeGross()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        int bookingId = 8_100 + Random.Shared.Next(1_000);
        await using (var seed = CreateContext())
        {
            var authorization = await SeedAuthorizationAsync(seed);
            var escrow = EscrowEntity.CreateAuthorized(
                bookingId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                authorization.Id,
                new CommissionCalculation(Currency.Gbp, 5000, 1000, 800, 200, 2000, 6000),
                $"pi_escrow_{Guid.NewGuid():N}");
            escrow.Confirm();
            escrow.CreatedBy = "integration";
            escrow.CreatedAt = DateTime.UtcNow;
            seed.Escrows.Add(escrow);
            await seed.SaveChangesAsync();
        }

        var paymentManager = BarrierRefundManager(participants: 2);

        async Task<Result<Refund?>> RefundAsync(long grossMinor)
        {
            await using var context = CreateContext();
            var service = new EscrowService(
                paymentManager.Object,
                new EscrowRepository(context),
                Mock.Of<IPayoutAccountRepository>(),
                Mock.Of<ILedgerService>(),
                new UnitOfWork(context),
                Mock.Of<ICommissionService>(),
                new CommissionCalculator(),
                context,
                Options.Create(new PlatformFeeOptions { Fee = 0m }),
                TimeProvider.System,
                NullLogger<EscrowService>.Instance);
            return await service.RefundCommissionAuthorizedByBookingIdAsync(bookingId, grossMinor, Currency.Gbp);
        }

        var results = await Task.WhenAll(RefundAsync(3000), RefundAsync(3000));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => r.IsFailed));

        await using var verification = CreateContext();
        var committedGross = await verification.PaymentRefunds
            .Where(r => r.EscrowId != null)
            .SumAsync(r => r.GrossRefundedMinor);
        Assert.Equal(3000, committedGross);
        Assert.True(committedGross <= 5000);
    }

    [Fact]
    public async Task ConcurrentSettlementPartialRefunds_OfDifferentAmounts_CannotOverRefundPastPayeeGross()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        int bookingId = 9_100 + Random.Shared.Next(1_000);
        await using (var seed = CreateContext())
        {
            var authorization = await SeedAuthorizationAsync(seed);
            var settlement = SettlementTransactionEntity.CreateAuthorized(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"pi_settlement_{Guid.NewGuid():N}",
                new CommissionCalculation(Currency.Gbp, 5000, 1000, 800, 200, 2000, 6000),
                TransactionStatus.Complete,
                bookingId,
                authorization.Id);
            settlement.CreatedBy = "integration";
            settlement.CreatedAt = DateTime.UtcNow;
            seed.SettlementTransactions.Add(settlement);
            await seed.SaveChangesAsync();
        }

        var paymentManager = BarrierRefundManager(participants: 2);

        async Task<Result<Refund?>> RefundAsync(long grossMinor)
        {
            await using var context = CreateContext();
            var service = new ManagerPaymentService(
                paymentManager.Object,
                Mock.Of<IStripeAccountClient>(),
                Mock.Of<IStripeHoldClient>(),
                Mock.Of<IPayoutAccountRepository>(),
                new TransactionRepository(context),
                Mock.Of<ICommissionService>(),
                new CommissionCalculator(),
                Mock.Of<ILedgerService>(),
                new UnitOfWork(context),
                context,
                TimeProvider.System,
                Options.Create(new PlatformFeeOptions { Fee = 0m }));
            return await service.RefundCommissionAuthorizedByBookingIdAsync(bookingId, grossMinor, Currency.Gbp);
        }

        var results = await Task.WhenAll(RefundAsync(3000), RefundAsync(3000));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => r.IsFailed));

        await using var verification = CreateContext();
        var committedGross = await verification.PaymentRefunds
            .Where(r => r.SettlementTransactionId != null)
            .SumAsync(r => r.GrossRefundedMinor);
        Assert.Equal(3000, committedGross);
        Assert.True(committedGross <= 5000);
    }

    // Blocks each in-flight Stripe refund until every participant has loaded the aggregate (and so read the
    // same concurrency token), guaranteeing the read-check-write windows overlap. Unique Stripe ids keep the
    // committed refund distinct; the concurrency token — not Stripe — is what stops the second commit.
    private static Mock<IPaymentManager> BarrierRefundManager(int participants)
    {
        var mock = new Mock<IPaymentManager>();
        var allArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        mock
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (RefundRequest _, CancellationToken __) =>
            {
                if (Interlocked.Increment(ref arrived) == participants)
                    allArrived.SetResult();
                await allArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
                return Result.Ok(new Refund($"re_{Guid.NewGuid():N}"));
            });
        return mock;
    }

    private static async Task<CommissionAuthorizationEntity> SeedAuthorizationAsync(PaymentDbContext context)
    {
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(), $"integration-{Guid.NewGuid():N}", Currency.Gbp, 500, DateTimeOffset.UtcNow);
        var authorization = CommissionAuthorizationEntity.Create(
            configuration.Id, $"booking:{Guid.NewGuid():N}", $"payer:{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        context.AddRange(configuration, authorization);
        await context.SaveChangesAsync();
        return authorization;
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
