using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionAuthorizationClaimGuardTests
{
    private readonly Mock<IPaymentManager> paymentManager = new();
    private readonly Mock<IEscrowRepository> escrowRepository = new();
    private readonly Mock<ITransactionRepository> transactionRepository = new();
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository = new();
    private readonly Mock<ILedgerService> ledger = new();
    private readonly Mock<IStripeAccountClient> stripeAccountClient = new();
    private readonly Mock<IStripeHoldClient> stripeHoldClient = new();
    private readonly Mock<ICommissionService> commissionService = new();

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();
    private readonly Guid commissionAuthorizationId = Guid.NewGuid();

    public CommissionAuthorizationClaimGuardTests()
    {
        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));
        ledger
            .Setup(l => l.StageAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var claimed = new Dictionary<Guid, CommissionAuthorizationConsumer>();
        commissionService
            .Setup(s => s.ClaimAuthorizationAsync(
                It.IsAny<Guid>(),
                It.IsAny<CommissionAuthorizationConsumer>(),
                It.IsAny<CancellationToken>()))
            .Returns<Guid, CommissionAuthorizationConsumer, CancellationToken>((authId, consumer, _) =>
            {
                if (claimed.TryGetValue(authId, out var existing))
                    return Task.FromResult(existing == consumer
                        ? Result.Ok()
                        : Result.Fail("commission_authorization_already_consumed"));
                claimed[authId] = consumer;
                return Task.FromResult(Result.Ok());
            });

        var authorized = AuthorizedCommissionFor();
        commissionService
            .Setup(s => s.CalculateAuthorizedAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Currency>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(authorized));
    }

    [Fact]
    public async Task OneAuthorization_CannotBackBothEscrowAndSettlement_SecondConsumerRejectedWithoutStripe()
    {
        var escrow = BuildEscrowService();
        var manager = BuildManagerPaymentService();

        escrowRepository
            .Setup(r => r.GetByCommissionAuthorizationIdAsync(commissionAuthorizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity?)null);
        transactionRepository
            .Setup(r => r.GetSettlementByCommissionAuthorizationIdAsync(commissionAuthorizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SettlementTransactionEntity?)null);
        paymentManager
            .Setup(p => p.HoldAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(),
                It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_escrow", RequiresAction = false }));
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity e, CancellationToken _) => e);

        var deposit = await escrow.DepositCommissionAuthorizedAsync(
            payerId, payeeId, grossMinor: 5000, Currency.Gbp, "pm_test", PaymentSession.OnSession,
            bookingId: 7, commissionAuthorizationId, "booking:7",
            expectedCommissionMinor: 1000, expectedPayerTotalMinor: 6000, stripeSetupIntentId: null);

        Assert.True(deposit.IsSuccess);
        paymentManager.Verify(
            p => p.HoldAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(),
                It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var settlement = await manager.PayCommissionAuthorizedAsync(
            payerId, payeeId, grossMinor: 5000, Currency.Gbp, "pm_test", PaymentSession.OnSession,
            bookingId: 7, commissionAuthorizationId, "booking:7",
            expectedCommissionMinor: 1000, expectedPayerTotalMinor: 6000, stripeSetupIntentId: null);

        Assert.True(settlement.IsFailed);
        Assert.Contains(settlement.Errors, e => e.Message == "commission_authorization_already_consumed");
        paymentManager.Verify(
            p => p.SettleAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<Money>(), It.IsAny<string>(),
                It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        transactionRepository.Verify(
            r => r.AddAsync(It.IsAny<SettlementTransactionEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private EscrowService BuildEscrowService() =>
        new(
            paymentManager.Object,
            escrowRepository.Object,
            payoutAccountRepository.Object,
            ledger.Object,
            new FakeUnitOfWork(),
            commissionService.Object,
            new CommissionCalculator(),
            TestPaymentDbContext.Unopened(),
            Options.Create(new PlatformFeeOptions { Fee = 0m }),
            new FakeTimeProvider(),
            NullLogger<EscrowService>.Instance);

    private ManagerPaymentService BuildManagerPaymentService() =>
        new(
            paymentManager.Object,
            stripeAccountClient.Object,
            stripeHoldClient.Object,
            payoutAccountRepository.Object,
            transactionRepository.Object,
            commissionService.Object,
            new CommissionCalculator(),
            ledger.Object,
            new FakeUnitOfWork(),
            TestPaymentDbContext.Unopened(),
            new FakeTimeProvider(),
            Options.Create(new PlatformFeeOptions { Fee = 0m }));

    private AuthorizedCommission AuthorizedCommissionFor()
    {
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(), $"v-{Guid.NewGuid():N}", Currency.Gbp, 500, DateTimeOffset.UtcNow);
        var authorization = CommissionAuthorizationEntity.Create(
            configuration.Id, "booking:7", payerId.ToString(), DateTimeOffset.UtcNow);
        var calculation = new CommissionCalculation(Currency.Gbp, 5000, 1000, 800, 200, 2000, 6000);
        return new AuthorizedCommission(authorization, configuration, calculation);
    }

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
