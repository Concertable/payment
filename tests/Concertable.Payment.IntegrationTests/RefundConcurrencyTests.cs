using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Testing.Integration;
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
    public async Task ConcurrentEscrowPartialRefunds_OfDifferentAmounts_LoserMakesNoStripeCallAndCannotOverRefund()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        int bookingId = 8_100 + Random.Shared.Next(1_000);
        await using (var seed = CreateContext())
        {
            var binding = await SeedAuthorizationAsync(seed);
            var escrow = EscrowEntity.CreateBound(
                bookingId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                binding.Id,
                new Concertable.Payment.Domain.CommissionCalculation(
                    Currency.Gbp,
                    5000,
                    1000,
                    800,
                    200,
                    Percentage.From(20m),
                    6000),
                $"pi_escrow_{Guid.NewGuid():N}");
            escrow.Confirm();
            escrow.CreatedBy = "integration";
            escrow.CreatedAt = DateTime.UtcNow;
            seed.Escrows.Add(escrow);
            await seed.SaveChangesAsync();
        }

        var stripe = RecordingRefundManager();
        var gate = new StartGate(participants: 2);

        async Task<Result<Option<Refund>, EscrowRefundError>> RefundAsync(long grossMinor)
        {
            await using var context = CreateContext();
            var service = new EscrowService(
                stripe.Object,
                new EscrowRepository(context),
                Mock.Of<IPayoutAccountRepository>(),
                Mock.Of<ILedgerService>(),
                new UnitOfWork(context),
                Mock.Of<ICommissionService>(),
                new CommissionCalculator(),
                Options.Create(new PlatformFeeOptions { Fee = 0m }),
                TimeProvider.System,
                NullLogger<EscrowService>.Instance);
            await gate.WaitAsync();
            return await service.RefundBoundCommissionByBookingIdAsync(bookingId, grossMinor, Currency.Gbp);
        }

        var results = await Task.WhenAll(RefundAsync(3000), RefundAsync(2500));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => r.IsFailure));

        stripe.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await using var verification = CreateContext();
        var rows = await verification.PaymentRefunds
            .Where(r => r.EscrowId != null)
            .ToListAsync();
        var committed = Assert.Single(rows);
        Assert.Equal(PaymentRefundStatus.Completed, committed.Status);
        Assert.Contains(committed.GrossRefundedMinor, new long[] { 3000, 2500 });
        Assert.True(committed.GrossRefundedMinor <= 5000);

        var escrowRow = await verification.Escrows.SingleAsync(e => e.BookingId == bookingId);
        Assert.Equal(committed.GrossRefundedMinor, escrowRow.RefundedGrossMinor);
    }

    [Fact]
    public async Task ConcurrentSettlementPartialRefunds_OfDifferentAmounts_LoserMakesNoStripeCallAndCannotOverRefund()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        int bookingId = 9_100 + Random.Shared.Next(1_000);
        await using (var seed = CreateContext())
        {
            var binding = await SeedAuthorizationAsync(seed);
            var settlement = SettlementTransactionEntity.CreateBound(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"pi_settlement_{Guid.NewGuid():N}",
                new Concertable.Payment.Domain.CommissionCalculation(
                    Currency.Gbp,
                    5000,
                    1000,
                    800,
                    200,
                    Percentage.From(20m),
                    6000),
                TransactionStatus.Complete,
                bookingId,
                binding.Id);
            settlement.CreatedBy = "integration";
            settlement.CreatedAt = DateTime.UtcNow;
            seed.SettlementTransactions.Add(settlement);
            await seed.SaveChangesAsync();
        }

        var stripe = RecordingRefundManager();
        var gate = new StartGate(participants: 2);

        async Task<Result<Option<Refund>, EscrowRefundError>> RefundAsync(long grossMinor)
        {
            await using var context = CreateContext();
            var service = new ManagerPaymentService(
                stripe.Object,
                Mock.Of<IStripeAccountClient>(),
                Mock.Of<IStripeHoldClient>(),
                Mock.Of<IPayoutAccountRepository>(),
                new TransactionRepository(context),
                Mock.Of<ICommissionService>(),
                new CommissionCalculator(),
                Mock.Of<ILedgerService>(),
                new UnitOfWork(context),
                TimeProvider.System,
                Options.Create(new PlatformFeeOptions { Fee = 0m }));
            await gate.WaitAsync();
            return await service.RefundBoundCommissionByBookingIdAsync(bookingId, grossMinor, Currency.Gbp);
        }

        var results = await Task.WhenAll(RefundAsync(3000), RefundAsync(2500));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => r.IsFailure));

        stripe.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await using var verification = CreateContext();
        var rows = await verification.PaymentRefunds
            .Where(r => r.SettlementTransactionId != null)
            .ToListAsync();
        var committed = Assert.Single(rows);
        Assert.Equal(PaymentRefundStatus.Completed, committed.Status);
        Assert.Contains(committed.GrossRefundedMinor, new long[] { 3000, 2500 });
        Assert.True(committed.GrossRefundedMinor <= 5000);

        var settlementRow = await verification.SettlementTransactions.SingleAsync(t => t.BookingId == bookingId);
        Assert.Equal(committed.GrossRefundedMinor, settlementRow.RefundedGrossMinor);
    }

    // The reservation guard is an atomic conditional UPDATE (RefundedGrossMinor + @gross <= PayeeGrossMinor):
    // the DB row lock serializes the two concurrent reservations, so the loser's UPDATE matches zero rows and
    // it returns a conflict BEFORE reaching the (guard-free) Stripe call. A plain recording mock therefore sees
    // exactly one refund, and cumulative gross can never exceed the escrow/settlement's payee gross.
    private static Mock<IPaymentManager> RecordingRefundManager()
    {
        var mock = new Mock<IPaymentManager>();
        mock
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<Refund, PaymentError>.Success(
                new Refund($"re_{Guid.NewGuid():N}")));
        return mock;
    }

    private sealed class StartGate
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int participants;
        private int arrived;

        public StartGate(int participants) => this.participants = participants;

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref arrived) == participants)
                gate.SetResult();
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private static async Task<CommissionBindingEntity> SeedAuthorizationAsync(PaymentDbContext context)
    {
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(),
            Percentage.From(5m),
            DateTimeOffset.UtcNow);
        var binding = CommissionBindingEntity.Create(
            configuration,
            Currency.Gbp,
            $"booking:{Guid.NewGuid():N}",
            $"payer:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        context.Add(configuration);
        context.Add(binding);
        await context.SaveChangesAsync();
        return binding;
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
