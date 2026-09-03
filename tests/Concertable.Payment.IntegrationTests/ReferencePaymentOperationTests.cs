using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Kernel.Identity;
using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.Lifecycle;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Handlers;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Payment.IntegrationTests.Fixtures;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Concertable.Payment.IntegrationTests;

public sealed class ReferencePaymentOperationTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public ReferencePaymentOperationTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task DepositAsync_ProviderUnavailable_ReplaysPendingOperationAfterRecovery()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var reference = Reference();
        string providerObjectId;
        await using (var setupContext = CreateContext())
        {
            await SeedPayerAsync(setupContext, payerId);
            providerObjectId = await CreatePaymentMethodAsync(
                setupContext,
                provider,
                reference,
                payerId);
        }

        var command = new DepositEscrowByReferenceCommand(
            Guid.CreateVersion7(),
            17,
            payerId,
            payeeId,
            5000,
            Currency.Gbp,
            reference,
            PaymentSession.OffSession);
        var escrow = new Mock<IEscrowService>();
        var bus = new Mock<IBus>();

        await using (var unavailableContext = CreateContext())
        {
            var handler = Handler(
                unavailableContext,
                new UnavailableRetrievalStripeSessionClient(provider),
                escrow,
                bus);

            await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() =>
                handler.HandleAsync(
                    command,
                    MessageEnvelope.Create<DepositEscrowByReferenceCommand>(DateTimeOffset.UtcNow)));

            Assert.Equal(
                FinancialOperationStatus.Pending,
                (await unavailableContext.FinancialOperations.SingleAsync(
                    operation => operation.Id == command.OperationId)).Status);
        }

        provider.SetStatus(providerObjectId, "succeeded");
        escrow
            .Setup(service => service.DepositAsync(
                payerId,
                payeeId,
                Money.Gbp(50),
                $"pm_fake_{providerObjectId}",
                PaymentSession.OffSession,
                17,
                command.OperationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EscrowDeposit(1, "pi_deposit", EscrowStatus.Held));
        bus
            .Setup(value => value.PublishAsync(
                It.IsAny<DepositEscrowSucceededEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using (var replayContext = CreateContext())
        {
            await Handler(replayContext, provider, escrow, bus).HandleAsync(
                command,
                MessageEnvelope.Create<DepositEscrowByReferenceCommand>(DateTimeOffset.UtcNow));

            Assert.Equal(
                FinancialOperationStatus.Succeeded,
                (await replayContext.FinancialOperations.SingleAsync(
                    operation => operation.Id == command.OperationId)).Status);
        }

        escrow.VerifyAll();
        bus.VerifyAll();
    }

    [Fact]
    public async Task CaptureAsync_AuthorizationReference_UsesResolvedProviderObject()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var operationId = Guid.CreateVersion7();
        var reference = Reference();
        var specification = PaymentSessionSpecification.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            reference.OperationType,
            reference.ConsumerCorrelation,
            payerId.ToString("D"),
            payeeId.ToString("D"),
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{payerId:N}",
            $"acct_{payeeId:N}");
        string providerObjectId;
        await using (var setupContext = CreateContext())
        {
            await SeedPayerAsync(setupContext, payerId);
            var created = await SessionService(setupContext, provider)
                .CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out _));
            providerObjectId = (await setupContext.PaymentSessionAttempts.SingleAsync(
                attempt => attempt.OperationId == operationId)).ProviderObjectId!;
        }
        provider.SetStatus(
            providerObjectId,
            "requires_capture",
            DateTimeOffset.UtcNow.AddDays(1));

        var command = new CaptureEscrowByReferenceCommand(
            Guid.CreateVersion7(),
            17,
            payerId,
            payeeId,
            5000,
            Currency.Gbp,
            reference);
        var escrow = new Mock<IEscrowService>();
        escrow
            .Setup(service => service.CaptureAsync(
                payerId,
                payeeId,
                Money.Gbp(50),
                providerObjectId,
                17,
                command.OperationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EscrowDeposit(1, providerObjectId, EscrowStatus.Held));
        var bus = new Mock<IBus>();
        bus
            .Setup(value => value.PublishAsync(
                It.IsAny<CaptureEscrowSucceededEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var context = CreateContext();
        await Handler(context, provider, escrow, bus).HandleAsync(
            command,
            MessageEnvelope.Create<CaptureEscrowByReferenceCommand>(DateTimeOffset.UtcNow));

        Assert.Equal(
            FinancialOperationStatus.Succeeded,
            (await context.FinancialOperations.SingleAsync(
                operation => operation.Id == command.OperationId)).Status);
        escrow.VerifyAll();
        bus.VerifyAll();
    }

    [Fact]
    public async Task PayAsync_PaymentMethodReference_UsesResolvedPaymentMethodAndPersistsSettlement()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var reference = Reference();
        string providerObjectId;
        await using (var setupContext = CreateContext())
        {
            await SeedPayerAsync(setupContext, payerId);
            providerObjectId = await CreatePaymentMethodAsync(
                setupContext,
                provider,
                reference,
                payerId);
        }
        provider.SetStatus(providerObjectId, "succeeded");

        var paymentManager = new Mock<IPaymentManager>();
        var stripeAccountClient = new Mock<IStripeAccountClient>();
        var stripeHoldClient = new Mock<IStripeHoldClient>();
        var commissionService = new Mock<ICommissionService>();
        var ledger = new Mock<ILedgerService>();
        var operationId = Guid.CreateVersion7();
        paymentManager
            .Setup(manager => manager.SettleAsync(
                operationId,
                payerId,
                payeeId,
                Money.Gbp(50),
                Money.Gbp(50),
                $"pm_fake_{providerObjectId}",
                PaymentSession.OffSession,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOutcome
            {
                TransactionId = "pi_reference",
                RequiresAction = true
            });

        await using var context = CreateContext();
        var service = new ManagerPaymentService(
            paymentManager.Object,
            stripeAccountClient.Object,
            stripeHoldClient.Object,
            new PayoutAccountRepository(context),
            new TransactionRepository(context),
            commissionService.Object,
            new CommissionCalculator(),
            ledger.Object,
            new UnitOfWork(context),
            Resolver(context, provider),
            TimeProvider.System,
            Options.Create(new PlatformFeeOptions { Fee = 0 }));

        var result = await service.PayAsync(
            operationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            reference,
            PaymentSession.OffSession,
            17);

        Assert.True(result.TryGetValue(out var payment));
        Assert.Equal("pi_reference", payment.TransactionId);
        Assert.Equal(
            "pi_reference",
            (await context.SettlementTransactions.SingleAsync(
                transaction => transaction.OperationId == operationId)).PaymentIntentId);
        paymentManager.VerifyAll();
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private PaymentDbContext CreateContext()
    {
        var currentUser = new Mock<ICurrentUser>();
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .AddInterceptors(new AuditInterceptor(currentUser.Object, TimeProvider.System))
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }

    private static async Task SeedPayerAsync(PaymentDbContext context, Guid payerId)
    {
        var payer = PayoutAccountEntity.Create(payerId, $"{payerId:N}@example.com");
        payer.LinkCustomer($"cus_{payerId:N}");
        context.PayoutAccounts.Add(payer);
        await context.SaveChangesAsync();
    }

    private static async Task<string> CreatePaymentMethodAsync(
        PaymentDbContext context,
        FakeStripeSessionClient provider,
        PaymentOperationReference reference,
        Guid payerId)
    {
        var setup = await SessionService(context, provider).SetupPaymentMethodAsync(
            new(reference, PaymentSessionKind.PaymentMethodSetup, payerId));
        Assert.True(setup.TryGetValue(out _));
        return (await context.PaymentSessionOperations
            .Include(operation => operation.Attempts)
            .SingleAsync(operation => operation.OperationType == reference.OperationType
                && operation.ConsumerCorrelation == reference.ConsumerCorrelation))
            .CurrentAttempt.ProviderObjectId!;
    }

    private static PaymentSessionService SessionService(
        PaymentDbContext context,
        IStripeSessionClient provider)
    {
        var operationRepository = new PaymentSessionOperationRepository(context);
        var reconciliationService = Reconciliation(context);
        return new(
            operationRepository,
            new PayoutAccountRepository(context),
            reconciliationService,
            provider,
            new PaymentOperationResolver(operationRepository, reconciliationService, provider),
            TimeProvider.System);
    }

    private static PaymentOperationResolver Resolver(
        PaymentDbContext context,
        IStripeSessionClient provider)
    {
        var operationRepository = new PaymentSessionOperationRepository(context);
        return new(operationRepository, Reconciliation(context), provider);
    }

    private static PaymentSessionReconciliationService Reconciliation(PaymentDbContext context) =>
        new(
            new PaymentSessionAttemptRepository(context),
            new UnitOfWork(context),
            new PaymentSessionStateMachine(),
            TimeProvider.System);

    private static FinancialOperationHandler Handler(
        PaymentDbContext context,
        IStripeSessionClient provider,
        Mock<IEscrowService> escrow,
        Mock<IBus> bus) =>
        new(
            escrow.Object,
            new FinancialOperationRepository(context),
            new UnitOfWork(context),
            bus.Object,
            new OutboxUnitOfWorkBehavior(context, new TestDbContextAccessor()),
            Resolver(context, provider),
            TimeProvider.System);

    private static PaymentOperationReference Reference() =>
        new("applicationApply", $"application:{Guid.CreateVersion7():N}");

    private sealed class TestDbContextAccessor : IDbContextAccessor
    {
        public DbContext? Context { get; set; }
    }
}
