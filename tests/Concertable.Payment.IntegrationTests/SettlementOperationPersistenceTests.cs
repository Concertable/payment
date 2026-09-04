using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Kernel.Identity;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Payment.Infrastructure;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Reunion;

namespace Concertable.Payment.IntegrationTests;

public sealed class SettlementOperationPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public SettlementOperationPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task OperationReplayState_RoundTripsForSettlementAndEscrow()
    {
        await using (var migration = CreateContext())
            await migration.Database.MigrateAsync();

        var chargeOperationId = Guid.CreateVersion7();
        var releaseOperationId = Guid.CreateVersion7();
        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var settlementReference = new PaymentOperationReference("settlement", "order:42");
        var escrowReference = new PaymentOperationReference("escrow", "order:43");
        var chargeFingerprint = SettlementOperationFingerprint.CreateCharge(
            chargeOperationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(12),
            "pm_test",
            PaymentSession.OnSession,
            settlementReference);
        var settlement = SettlementTransactionEntity.CreateForOperation(
            payerId,
            payeeId,
            "pi_operation",
            6200,
            1200,
            TransactionStatus.Pending,
            settlementReference,
            chargeOperationId,
            chargeFingerprint,
            true);
        var escrow = EscrowEntity.Create(
            escrowReference,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(0),
            "pi_escrow");
        escrow.Confirm();

        await using (var seed = CreateContext())
        {
            seed.Add(settlement);
            seed.Add(escrow);
            await seed.SaveChangesAsync();

            var releaseFingerprint = SettlementOperationFingerprint.CreateRelease(releaseOperationId, escrow);
            escrow.BeginRelease(releaseOperationId, releaseFingerprint);
            await seed.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var storedSettlement = await verification.SettlementTransactions
            .SingleAsync(value => value.OperationId == chargeOperationId);
        var storedEscrow = await verification.Escrows
            .SingleAsync(value => value.ReleaseOperationId == releaseOperationId);

        Assert.Equal(chargeFingerprint.Version, storedSettlement.OperationFingerprintVersion);
        Assert.Equal(chargeFingerprint.Value, storedSettlement.OperationFingerprint);
        Assert.True(storedSettlement.RequiresAction);
        Assert.Equal(SettlementOperationFingerprint.CurrentVersion, storedEscrow.ReleaseOperationFingerprintVersion);
        Assert.NotNull(storedEscrow.ReleaseOperationFingerprint);
    }

    [Fact]
    public async Task ReserveReleaseAsync_ConcurrentDifferentOperations_OnlyOneOperationWins()
    {
        await using (var migration = CreateContext())
            await migration.Database.MigrateAsync();

        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var escrow = EscrowEntity.Create(
            new("escrow", "order:44"),
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(0),
            "pi_release_race");
        escrow.Confirm();
        await using (var seed = CreateContext())
        {
            seed.Add(escrow);
            await seed.SaveChangesAsync();
        }

        var firstOperationId = Guid.CreateVersion7();
        var secondOperationId = Guid.CreateVersion7();
        var firstFingerprint = SettlementOperationFingerprint.CreateRelease(firstOperationId, escrow);
        var secondFingerprint = SettlementOperationFingerprint.CreateRelease(secondOperationId, escrow);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstRepository = new EscrowRepository(firstContext);
        var secondRepository = new EscrowRepository(secondContext);

        var reservations = await Task.WhenAll(
            firstRepository.ReserveReleaseAsync(escrow.Id, firstOperationId, firstFingerprint),
            secondRepository.ReserveReleaseAsync(escrow.Id, secondOperationId, secondFingerprint));

        Assert.DoesNotContain(reservations, value => value.Conflict);
        var winner = Assert.Single(reservations.Select(value => value.Escrow!.ReleaseOperationId).Distinct());
        Assert.Contains(winner, new Guid?[] { firstOperationId, secondOperationId });
        await using var verification = CreateContext();
        Assert.Equal(winner, (await verification.Escrows.SingleAsync(value => value.Id == escrow.Id)).ReleaseOperationId);
    }

    [Fact]
    public async Task ReserveReleaseAsync_OperationAlreadyBoundToAnotherEscrow_ReturnsConflict()
    {
        await using (var migration = CreateContext())
            await migration.Database.MigrateAsync();

        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var firstEscrow = EscrowEntity.Create(
            new("escrow", "order:46"), payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_first_release");
        var secondEscrow = EscrowEntity.Create(
            new("escrow", "order:47"), payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_second_release");
        firstEscrow.Confirm();
        secondEscrow.Confirm();
        await using (var seed = CreateContext())
        {
            seed.AddRange(firstEscrow, secondEscrow);
            await seed.SaveChangesAsync();
        }

        var operationId = Guid.CreateVersion7();
        var firstFingerprint = SettlementOperationFingerprint.CreateRelease(operationId, firstEscrow);
        var secondFingerprint = SettlementOperationFingerprint.CreateRelease(operationId, secondEscrow);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstRepository = new EscrowRepository(firstContext);
        var secondRepository = new EscrowRepository(secondContext);

        var firstReservation = await firstRepository.ReserveReleaseAsync(
            firstEscrow.Id,
            operationId,
            firstFingerprint);
        var secondReservation = await secondRepository.ReserveReleaseAsync(
            secondEscrow.Id,
            operationId,
            secondFingerprint);

        Assert.False(firstReservation.Conflict);
        Assert.Equal(operationId, firstReservation.Escrow!.ReleaseOperationId);
        Assert.True(secondReservation.Conflict);
        await using var verification = CreateContext();
        Assert.Null((await verification.Escrows.SingleAsync(value => value.Id == secondEscrow.Id)).ReleaseOperationId);
    }

    [Fact]
    public async Task PayAsync_ConcurrentSameOperation_ConvergesOnPersistedOutcome()
    {
        await using (var migration = CreateContext())
            await migration.Database.MigrateAsync();

        var operationId = Guid.CreateVersion7();
        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var reference = new PaymentOperationReference("settlement", "order:45");
        var paymentMethod = new PaymentOperationReference("paymentMethod", "wallet:45");
        var outcome = new PaymentOutcome
        {
            TransactionId = "pi_concurrent_operation",
            RequiresAction = true,
            ClientSecret = "pi_concurrent_secret"
        };
        var bothProviderCallsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCalls = 0;
        var paymentManager = new Mock<IPaymentManager>();
        paymentManager
            .Setup(value => value.SettleAsync(
                operationId,
                payerId,
                payeeId,
                Money.Gbp(62),
                Money.Gbp(50),
                "pm_test",
                PaymentSession.OnSession,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref providerCalls) == 2)
                    bothProviderCallsEntered.SetResult();
                await bothProviderCallsEntered.Task;
                return Result<PaymentOutcome, ChargeError>.Success(outcome);
            });
        paymentManager
            .Setup(value => value.GetPaymentOutcomeAsync(
                outcome.TransactionId,
                PaymentSession.OnSession,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Success(outcome));
        var payoutAccount = PayoutAccountEntity.Create(payerId, "payer@test.com");
        payoutAccount.LinkCustomer("cus_test");
        var payoutAccounts = new Mock<IPayoutAccountRepository>();
        payoutAccounts
            .Setup(value => value.GetByOwnerIdAsync(payerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payoutAccount);

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = CreateSettlementService(firstContext, paymentManager.Object, payoutAccounts.Object, paymentMethod, payerId);
        var second = CreateSettlementService(secondContext, paymentManager.Object, payoutAccounts.Object, paymentMethod, payerId);

        var results = await Task.WhenAll(
            first.PayAsync(operationId, reference, payerId, payeeId, Money.Gbp(50), paymentMethod, PaymentSession.OnSession),
            second.PayAsync(operationId, reference, payerId, payeeId, Money.Gbp(50), paymentMethod, PaymentSession.OnSession));

        Assert.All(results, result =>
        {
            Assert.True(result.TryGetValue(out var value));
            Assert.Equal(outcome.TransactionId, value.TransactionId);
            Assert.Equal(outcome.ClientSecret, value.ClientSecret);
        });
        Assert.Equal(2, providerCalls);
        await using var verification = CreateContext();
        Assert.Single(await verification.SettlementTransactions
            .Where(value => value.OperationId == operationId)
            .ToListAsync());
    }

    private static SettlementService CreateSettlementService(
        PaymentDbContext context,
        IPaymentManager paymentManager,
        IPayoutAccountRepository payoutAccounts,
        PaymentOperationReference paymentMethod,
        Guid payerId)
    {
        var resolver = new Mock<IPaymentOperationResolver>();
        resolver
            .Setup(value => value.ResolvePaymentMethodAsync(
                paymentMethod,
                payerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, PaymentOperationError>.Success("pm_test"));
        return new(
            paymentManager,
            payoutAccounts,
            new TransactionRepository(context),
            Mock.Of<ICommissionService>(),
            new CommissionCalculator(),
            Mock.Of<ILedgerService>(),
            new UnitOfWork(context),
            resolver.Object,
            TimeProvider.System,
            Options.Create(new PlatformFeeOptions { Fee = 12 }));
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .AddInterceptors(new AuditInterceptor(Mock.Of<ICurrentUser>(), TimeProvider.System))
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
