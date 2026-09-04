using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Handlers;
using Concertable.Payment.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class ReferencePaymentOperationTests : IClassFixture<ApiFixture>, IAsyncLifetime
{
    private readonly ApiFixture fixture;

    public ReferencePaymentOperationTests(ApiFixture fixture)
    {
        this.fixture = fixture;
    }

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DepositAsync_ProviderUnavailable_ReplaysPendingOperationAfterRecovery()
    {
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var reference = Reference();
        await SeedAccountsAsync(payerId, payeeId);
        var providerObjectId = await CreatePaymentMethodAsync(reference, payerId);
        var command = new DepositEscrowCommand(
            Guid.CreateVersion7(),
            reference,
            payerId,
            payeeId,
            5000,
            Currency.Gbp,
            reference,
            PaymentSession.OffSession);
        fixture.SetProviderRetrievalUnavailable(true);

        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() =>
            DispatchAsync(command));
        Assert.Equal(
            FinancialOperationStatus.Pending,
            await FinancialOperationStatusAsync(command.OperationId));

        fixture.SetProviderRetrievalUnavailable(false);
        fixture.SetProviderStatus(providerObjectId, "succeeded");

        await DispatchAsync(command);

        var persisted = await fixture.RunAsync(async (PaymentDbContext context) =>
        {
            var operation = await context.FinancialOperations.SingleAsync(
                value => value.Id == command.OperationId);
            var escrow = await context.Escrows.SingleAsync(
                value => value.OperationType == reference.OperationType
                    && value.ClientReference == reference.ClientReference);
            return (OperationStatus: operation.Status, EscrowStatus: escrow.Status);
        });
        Assert.Equal(FinancialOperationStatus.Succeeded, persisted.OperationStatus);
        Assert.Equal(EscrowStatus.Held, persisted.EscrowStatus);
    }

    [Fact]
    public async Task CaptureAsync_AuthorizationReference_UsesResolvedProviderObject()
    {
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var operationId = Guid.CreateVersion7();
        var reference = Reference();
        await SeedAccountsAsync(payerId, payeeId);
        var providerObjectId = await CreateAuthorizationAsync(
            operationId,
            reference,
            payerId,
            payeeId);
        fixture.SetProviderStatus(
            providerObjectId,
            "requires_capture",
            DateTimeOffset.UtcNow.AddDays(1));
        var command = new CaptureEscrowCommand(
            Guid.CreateVersion7(),
            reference,
            payerId,
            payeeId,
            5000,
            Currency.Gbp,
            reference);

        await DispatchAsync(command);

        var persisted = await fixture.RunAsync(async (PaymentDbContext context) =>
        {
            var operation = await context.FinancialOperations.SingleAsync(
                value => value.Id == command.OperationId);
            var escrow = await context.Escrows.SingleAsync(
                value => value.OperationType == reference.OperationType
                    && value.ClientReference == reference.ClientReference);
            return (OperationStatus: operation.Status, EscrowStatus: escrow.Status, escrow.ChargeId);
        });
        Assert.Equal(FinancialOperationStatus.Succeeded, persisted.OperationStatus);
        Assert.Equal(EscrowStatus.Held, persisted.EscrowStatus);
        Assert.Equal(providerObjectId, persisted.ChargeId);
    }

    [Fact]
    public async Task PayAsync_PaymentMethodReference_UsesResolvedPaymentMethodAndPersistsSettlement()
    {
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var reference = Reference();
        await SeedAccountsAsync(payerId, payeeId);
        var providerObjectId = await CreatePaymentMethodAsync(reference, payerId);
        fixture.SetProviderStatus(providerObjectId, "succeeded");
        var operationId = Guid.CreateVersion7();

        var settlementReference = SettlementReference();
        var result = await fixture.RunAsync((ISettlementService service) =>
            service.PayAsync(
                operationId,
                settlementReference,
                payerId,
                payeeId,
                Money.Gbp(50),
                reference,
                PaymentSession.OffSession));

        Assert.True(result.TryGetValue(out var payment));
        var persistedPaymentIntentId = await fixture.RunAsync((PaymentDbContext context) =>
            context.SettlementTransactions
                .Where(transaction => transaction.OperationId == operationId)
                .Select(transaction => transaction.PaymentIntentId)
                .SingleAsync());
        Assert.Equal(payment.TransactionId, persistedPaymentIntentId);
    }

    private Task DispatchAsync(DepositEscrowCommand command) =>
        fixture.RunAsync((IIntegrationCommandHandler<DepositEscrowCommand> handler) =>
            handler.HandleAsync(
                command,
                MessageEnvelope.Create<DepositEscrowCommand>(DateTimeOffset.UtcNow)));

    private Task DispatchAsync(CaptureEscrowCommand command) =>
        fixture.RunAsync((IIntegrationCommandHandler<CaptureEscrowCommand> handler) =>
            handler.HandleAsync(
                command,
                MessageEnvelope.Create<CaptureEscrowCommand>(DateTimeOffset.UtcNow)));

    private Task<FinancialOperationStatus> FinancialOperationStatusAsync(Guid operationId) =>
        fixture.RunAsync((PaymentDbContext context) =>
            context.FinancialOperations
                .Where(operation => operation.Id == operationId)
                .Select(operation => operation.Status)
                .SingleAsync());

    private Task SeedAccountsAsync(Guid payerId, Guid payeeId) =>
        fixture.RunAsync(async (PaymentDbContext context) =>
        {
            var payer = PayoutAccountEntity.Create(payerId, $"{payerId:N}@example.com");
            payer.LinkCustomer($"cus_{payerId:N}");
            var payee = PayoutAccountEntity.Create(payeeId, $"{payeeId:N}@example.com");
            payee.LinkAccount($"acct_{payeeId:N}");
            context.PayoutAccounts.AddRange(payer, payee);
            await context.SaveChangesAsync();
        });

    private async Task<string> CreatePaymentMethodAsync(
        PaymentOperationReference reference,
        Guid payerId)
    {
        var setup = await fixture.RunAsync((IPaymentSessionService service) =>
            service.SetupPaymentMethodAsync(
                new(reference, PaymentSessionKind.PaymentMethodSetup, payerId, "recurring-payment-v1")));
        Assert.True(setup.TryGetValue(out _));
        return await CurrentProviderObjectIdAsync(reference);
    }

    private async Task<string> CreateAuthorizationAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId)
    {
        var request = new PaymentSessionOperationRequest(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OnSession,
            reference.OperationType,
            reference.ClientReference,
            payerId,
            payeeId,
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination);
        var created = await fixture.RunAsync((IPaymentSessionService service) =>
            service.CreateAsync(request));
        Assert.True(created.TryGetValue(out _));
        return await CurrentProviderObjectIdAsync(reference);
    }

    private Task<string> CurrentProviderObjectIdAsync(PaymentOperationReference reference) =>
        fixture.RunAsync((PaymentDbContext context) =>
            context.PaymentSessionOperations
                .Where(operation => operation.OperationType == reference.OperationType
                    && operation.ClientReference == reference.ClientReference)
                .SelectMany(operation => operation.Attempts
                    .Where(attempt => attempt.Revision == operation.CurrentRevision)
                    .Select(attempt => attempt.ProviderObjectId!))
                .SingleAsync());

    private static PaymentOperationReference Reference() =>
        new("purchase", $"order:{Guid.CreateVersion7():N}");

    private static PaymentOperationReference SettlementReference() =>
        new("settlement", $"settlement:{Guid.CreateVersion7():N}");
}
