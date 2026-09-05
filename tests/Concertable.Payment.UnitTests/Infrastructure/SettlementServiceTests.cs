using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Reunion;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class SettlementServiceTests
{
    private readonly Mock<IPaymentManager> paymentManager = new();
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository = new();
    private readonly Mock<ITransactionRepository> transactionRepository = new();
    private readonly Mock<ICommissionService> commissionService = new();
    private readonly Mock<ILedgerService> ledger = new();
    private readonly Mock<IPaymentOperationResolver> paymentOperationResolver = new();
    private readonly FakeTimeProvider timeProvider = new(
        new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero));
    private readonly List<LedgerPosting> postings = [];
    private readonly Guid payerId = Guid.CreateVersion7();
    private readonly Guid payeeId = Guid.CreateVersion7();
    private readonly PaymentOperationReference reference = new("settlement", "order:123");
    private readonly PaymentOperationReference paymentMethod = new("paymentMethod", "wallet:456");

    public SettlementServiceTests()
    {
        ledger
            .Setup(value => value.StageAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerPosting, CancellationToken>((posting, _) => postings.Add(posting))
            .Returns(Task.CompletedTask);
        payoutAccountRepository
            .Setup(value => value.GetByOwnerIdAsync(payerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWithCustomer());
        paymentOperationResolver
            .Setup(value => value.ResolvePaymentMethodAsync(
                paymentMethod,
                payerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, PaymentOperationError>.Success("pm_resolved"));
    }

    [Fact]
    public async Task PayAsync_WithPlatformFee_ChargesGrossPlusFeeAndRecordsReference()
    {
        var operationId = Guid.CreateVersion7();
        Money charged = default;
        Money paid = default;
        paymentManager
            .Setup(value => value.SettleAsync(
                operationId,
                payerId,
                payeeId,
                It.IsAny<Money>(),
                It.IsAny<Money>(),
                "pm_resolved",
                PaymentSession.OnSession,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, Money, Money, string, PaymentSession, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, _, _, charge, payee, _, _, _, _) =>
                {
                    charged = charge;
                    paid = payee;
                })
            .ReturnsAsync(Result<ProviderPaymentOutcome, ChargeError>.Success(new("pi_settlement")));
        SettlementTransactionEntity? captured = null;
        transactionRepository
            .Setup(value => value.AddAsync(It.IsAny<TransactionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionEntity, CancellationToken>((entity, _) => captured = (SettlementTransactionEntity)entity)
            .ReturnsAsync(() => captured!);

        var result = await Sut(12).PayAsync(
            operationId,
            reference,
            payerId,
            payeeId,
            Money.Gbp(50),
            paymentMethod,
            PaymentSession.OnSession);

        Assert.True(result.IsSuccess);
        Assert.Equal(Money.Gbp(62), charged);
        Assert.Equal(Money.Gbp(50), paid);
        Assert.NotNull(captured);
        Assert.Equal(reference.OperationType, captured.OperationType);
        Assert.Equal(reference.ClientReference, captured.ClientReference);
        Assert.Equal(6200, captured.Amount);
        Assert.Equal(1200, captured.CommissionGrossMinor);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, captured.CompletedAt);
        var posting = Assert.Single(postings);
        Assert.Equal(reference, posting.Reference);
        Assert.Equal(0, posting.SignedMinorUnitSum());
    }

    [Fact]
    public async Task PayAsync_ReplayedOperation_ReturnsPersistedOutcomeWithoutChargingAgain()
    {
        var operationId = Guid.CreateVersion7();
        var fingerprint = SettlementOperationFingerprint.CreateCharge(
            operationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(12),
            "pm_resolved",
            PaymentSession.OnSession,
            reference);
        var existing = SettlementTransactionEntity.CreateForOperation(
            payerId,
            payeeId,
            "pi_existing",
            6200,
            1200,
            TransactionStatus.Pending,
            reference,
            operationId,
            fingerprint,
            true);
        transactionRepository
            .Setup(value => value.GetSettlementByOperationIdAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        paymentManager
            .Setup(value => value.GetPaymentOutcomeAsync(
                "pi_existing",
                PaymentSession.OnSession,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProviderPaymentOutcome, PaymentError>.Success(
                new("pi_existing", true, "secret_existing")));

        var result = await Sut(12).PayAsync(
            operationId,
            reference,
            payerId,
            payeeId,
            Money.Gbp(50),
            paymentMethod,
            PaymentSession.OnSession);

        Assert.True(result.TryGetValue(out var payment));
        Assert.True(payment.RequiresAction);
        Assert.Equal("secret_existing", payment.ClientSecret);
        paymentManager.Verify(
            value => value.SettleAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Money>(),
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<PaymentSession>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PayAsync_ReusedOperationWithChangedReference_ReturnsConflict()
    {
        var operationId = Guid.CreateVersion7();
        var originalReference = new PaymentOperationReference("settlement", "order:original");
        var fingerprint = SettlementOperationFingerprint.CreateCharge(
            operationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(12),
            "pm_resolved",
            PaymentSession.OnSession,
            originalReference);
        transactionRepository
            .Setup(value => value.GetSettlementByOperationIdAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettlementTransactionEntity.CreateForOperation(
                payerId,
                payeeId,
                "pi_existing",
                6200,
                1200,
                TransactionStatus.Complete,
                originalReference,
                operationId,
                fingerprint,
                false));

        var result = await Sut(12).PayAsync(
            operationId,
            reference,
            payerId,
            payeeId,
            Money.Gbp(50),
            paymentMethod,
            PaymentSession.OnSession);

        Assert.True(result.TryGetError(out var error));
        Assert.IsType<PaymentMethodChargeError.OperationConflict>(error);
    }

    [Fact]
    public async Task PayAsync_AuthenticationRequired_ReturnsTypedFailure()
    {
        var operationId = Guid.CreateVersion7();
        paymentManager
            .Setup(value => value.SettleAsync(
                operationId,
                payerId,
                payeeId,
                It.IsAny<Money>(),
                It.IsAny<Money>(),
                "pm_resolved",
                PaymentSession.OffSession,
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProviderPaymentOutcome, ChargeError>.Failure(new ChargeError.AuthenticationRequired()));

        var result = await Sut(0).PayAsync(
            operationId,
            reference,
            payerId,
            payeeId,
            Money.Gbp(50),
            paymentMethod,
            PaymentSession.OffSession);

        Assert.True(result.TryGetError(out var error));
        Assert.IsType<PaymentMethodChargeError.AuthenticationRequired>(error);
    }

    private SettlementService Sut(decimal fee) =>
        new(
            paymentManager.Object,
            payoutAccountRepository.Object,
            transactionRepository.Object,
            commissionService.Object,
            new CommissionCalculator(),
            ledger.Object,
            new FakeUnitOfWork(),
            paymentOperationResolver.Object,
            timeProvider,
            Options.Create(new PlatformFeeOptions { Fee = fee }));

    private PayoutAccountEntity PayoutAccountWithCustomer()
    {
        var account = PayoutAccountEntity.Create(payerId, "payer@example.com");
        account.LinkCustomer("cus_test");
        return account;
    }
}
