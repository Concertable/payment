using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Reunion;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentSessionServiceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public PaymentSessionServiceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task CreateOrReplayAsync_FailureAfterProviderAcceptance_ReplayConvergesOnOneObject()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        provider.FailOnce(FakeStripeSessionFaultPoint.AfterProviderAcceptance);
        var specification = Specification(Guid.CreateVersion7());
        await using (var firstContext = CreateContext())
        {
            var first = await Service(firstContext, provider).CreateOrReplayAsync(specification);

            Assert.True(first.TryGetError(out PaymentOperationError? firstError));
            Assert.IsType<PaymentOperationError.ProviderUnavailable>(firstError);
        }

        await using var replayContext = CreateContext();
        var replay = await Service(replayContext, provider).CreateOrReplayAsync(specification);

        Assert.True(replay.TryGetValue(out PaymentSessionExecution? execution));
        Assert.Equal(PaymentOperationState.RequiresConfirmation, execution.State);
        Assert.Equal(1, provider.ProviderObjectCount);
        Assert.NotNull(execution.ClientSecret);
        Assert.NotNull(execution.CustomerSessionSecret);
        Assert.NotNull((await replayContext.PaymentSessionAttempts
            .SingleAsync(attempt => attempt.OperationId == specification.OperationId)).ProviderObjectId);
    }

    [Fact]
    public async Task CreateOrReplayAsync_FailureAfterBinding_ReplayReturnsFreshResponseSecrets()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        provider.FailOnce(FakeStripeSessionFaultPoint.BeforeCustomerSessionResponse);
        var specification = Specification(Guid.CreateVersion7());
        await using (var firstContext = CreateContext())
        {
            var first = await Service(firstContext, provider).CreateOrReplayAsync(specification);
            Assert.True(first.TryGetError(out _));
        }

        await using var replayContext = CreateContext();
        var replay = await Service(replayContext, provider).CreateOrReplayAsync(specification);

        Assert.True(replay.TryGetValue(out PaymentSessionExecution? execution));
        Assert.NotNull(execution.ClientSecret);
        Assert.NotNull(execution.CustomerSessionSecret);
        Assert.Equal(1, provider.ProviderObjectCount);
    }

    [Fact]
    public async Task CreateOrReplayAsync_ConcurrentSameRequest_ConvergesOnOneObject()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());

        async Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateAsync()
        {
            await using var context = CreateContext();
            return await Service(context, provider).CreateOrReplayAsync(specification);
        }

        var results = await Task.WhenAll(CreateAsync(), CreateAsync());

        Assert.All(results, result => Assert.True(result.TryGetValue(out _)));
        Assert.Equal(1, provider.ProviderObjectCount);
        Assert.Equal(
            results[0].Match(value => value.Identity.AttemptId, _ => Guid.Empty),
            results[1].Match(value => value.Identity.AttemptId, _ => Guid.Empty));
    }

    [Fact]
    public async Task CreateOrReplayAsync_ConflictingRequest_DoesNotCallProvider()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var operationId = Guid.CreateVersion7();
        await using var context = CreateContext();
        var service = Service(context, provider);
        var created = await service.CreateOrReplayAsync(Specification(operationId));

        var conflict = await service.CreateOrReplayAsync(Specification(operationId, 6000));

        Assert.True(created.TryGetValue(out _));
        Assert.True(conflict.TryGetError(out PaymentOperationError? error));
        Assert.IsType<PaymentOperationError.OperationConflict>(error);
        Assert.Equal(1, provider.ProviderObjectCount);
    }

    [Fact]
    public async Task RefreshAsync_UnknownStatus_DoesNotMutateNormalizedState()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        string providerObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out _));
            providerObjectId = (await createContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.OperationId == specification.OperationId)).ProviderObjectId!;
        }
        provider.SetStatus(providerObjectId, "future_provider_status");

        await using var refreshContext = CreateContext();
        var refreshed = await Service(refreshContext, provider).RefreshAsync(specification.OperationId);

        Assert.True(refreshed.TryGetError(out PaymentOperationError? error));
        Assert.IsType<PaymentOperationError.ProviderUnavailable>(error);
        Assert.Equal(
            PaymentOperationState.RequiresConfirmation,
            (await refreshContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.OperationId == specification.OperationId)).State);
    }

    [Fact]
    public async Task RetryAsync_EligibleAttempt_CancelsPredecessorAndCreatesOneSuccessor()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        Guid predecessorId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? createdExecution));
            predecessorId = createdExecution.Identity.AttemptId;
            var attempt = await createContext.PaymentSessionAttempts.SingleAsync(value => value.AttemptId == predecessorId);
            attempt.ApplyTransition(new(
                PaymentOperationTransitionDisposition.Applied,
                PaymentOperationState.Failed,
                "failed",
                DateTimeOffset.UtcNow.AddSeconds(1),
                null,
                PaymentOperationTerminalDisposition.AttemptTerminal,
                PaymentOperationRetryDisposition.CreateNewAttempt,
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
            await createContext.SaveChangesAsync();
        }

        await using var retryContext = CreateContext();
        var retried = await Service(retryContext, provider).RetryAsync(
            specification.OperationId,
            predecessorId,
            1);

        Assert.True(retried.TryGetValue(out PaymentSessionExecution? execution));
        Assert.Equal(2, execution.Identity.Revision);
        Assert.Equal(2, provider.ProviderObjectCount);
        Assert.Equal(
            2,
            await retryContext.PaymentSessionAttempts.CountAsync(
                attempt => attempt.OperationId == specification.OperationId));
    }

    [Fact]
    public void PersistenceModel_ContainsNoSecretColumns()
    {
        using var context = CreateContext();
        var properties = context.Model.GetEntityTypes()
            .Where(type => type.ClrType.Name.StartsWith("PaymentSession", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }

    private static PaymentSessionService Service(
        PaymentDbContext context,
        FakeStripeSessionClient provider) =>
        new(
            new PaymentSessionOperationRepository(context),
            new PaymentSessionAttemptRepository(context),
            provider,
            TimeProvider.System);

    private static PaymentSessionSpecification Specification(Guid operationId, long amountMinor = 5000) =>
        PaymentSessionSpecification.Create(
            operationId,
            PaymentSessionKind.Authorization,
            "escrow",
            $"booking:{operationId:N}",
            $"payer:{operationId:N}",
            $"payee:{operationId:N}",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"cus_{operationId:N}",
            $"acct_{operationId:N}");
}
