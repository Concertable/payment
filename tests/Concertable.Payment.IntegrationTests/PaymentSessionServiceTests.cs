using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
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
        string predecessorProviderObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? createdExecution));
            predecessorId = createdExecution.Identity.AttemptId;
            var attempt = await createContext.PaymentSessionAttempts.SingleAsync(value => value.AttemptId == predecessorId);
            predecessorProviderObjectId = attempt.ProviderObjectId!;
            attempt.ApplyTransition(new(
                PaymentOperationTransitionDisposition.Applied,
                PaymentOperationState.Failed,
                "failed",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                null,
                PaymentOperationTerminalDisposition.AttemptTerminal,
                PaymentOperationRetryDisposition.CreateNewAttempt,
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
            await createContext.SaveChangesAsync();
        }
        provider.SetDeclined(predecessorProviderObjectId);

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
    public async Task RetryAsync_PayeeOwner_ReturnsUnknownWithoutProviderCalls()
    {
        await MigrateAsync();
        var provider = new CountingStripeSessionClient(new FakeStripeSessionClient(TimeProvider.System));
        var operationId = Guid.CreateVersion7();
        var payerOwnerId = Guid.CreateVersion7();
        var payeeOwnerId = Guid.CreateVersion7();
        var specification = PaymentSessionSpecification.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"booking:{operationId:N}",
            payerOwnerId.ToString("D"),
            payeeOwnerId.ToString("D"),
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}");
        Guid predecessorId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? createdExecution));
            predecessorId = createdExecution.Identity.AttemptId;
        }
        var callCountBeforeRetry = provider.CallCount;

        await using var retryContext = CreateContext();
        var service = Service(retryContext, provider);
        var payeeRetry = await service.RetryAsync(
            new PaymentSessionRetryRequest(operationId, predecessorId, 1, payeeOwnerId));
        var unknownOperationRetry = await service.RetryAsync(
            new PaymentSessionRetryRequest(Guid.CreateVersion7(), predecessorId, 1, payeeOwnerId));

        Assert.True(payeeRetry.TryGetError(out PaymentOperationError? payeeError));
        Assert.True(unknownOperationRetry.TryGetError(out PaymentOperationError? unknownOperationError));
        Assert.IsType<PaymentOperationError.Unknown>(payeeError);
        Assert.Equal(unknownOperationError.GetType(), payeeError.GetType());
        Assert.Equal(callCountBeforeRetry, provider.CallCount);
    }

    [Theory]
    [InlineData("requires_confirmation", PaymentOperationState.RequiresConfirmation)]
    [InlineData("requires_capture", PaymentOperationState.Authorized)]
    public async Task RetryAsync_NonRetryableProviderState_DoesNotCancel(
        string providerStatus,
        PaymentOperationState expectedState)
    {
        await MigrateAsync();
        var innerProvider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        Guid attemptId;
        string providerObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, innerProvider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? execution));
            attemptId = execution.Identity.AttemptId;
            providerObjectId = (await createContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.AttemptId == attemptId)).ProviderObjectId!;
        }
        innerProvider.SetStatus(
            providerObjectId,
            providerStatus,
            expectedState == PaymentOperationState.Authorized
                ? DateTimeOffset.UtcNow.AddDays(1)
                : null);
        var provider = new CountingStripeSessionClient(innerProvider);

        await using var retryContext = CreateContext();
        var retried = await Service(retryContext, provider).RetryAsync(
            specification.OperationId,
            attemptId,
            1);

        Assert.True(retried.TryGetError(out PaymentOperationError? error));
        Assert.IsType<PaymentOperationError.OperationConflict>(error);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, provider.CancellationCount);
        Assert.Equal(expectedState, (await retryContext.PaymentSessionAttempts
            .SingleAsync(attempt => attempt.AttemptId == attemptId)).State);
    }

    [Theory]
    [InlineData("requires_capture", typeof(PaymentOperationError.OperationConflict))]
    [InlineData("future_provider_status", typeof(PaymentOperationError.ProviderUnavailable))]
    public async Task RetryAsync_PersistedFailureWithIneligibleProviderTruth_DoesNotCancelOrCreateSuccessor(
        string providerStatus,
        Type expectedErrorType)
    {
        await MigrateAsync();
        var innerProvider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        var failedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        Guid attemptId;
        string providerObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, innerProvider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? execution));
            attemptId = execution.Identity.AttemptId;
            var attempt = await createContext.PaymentSessionAttempts
                .SingleAsync(value => value.AttemptId == attemptId);
            providerObjectId = attempt.ProviderObjectId!;
            attempt.ApplyTransition(new(
                PaymentOperationTransitionDisposition.Applied,
                PaymentOperationState.Failed,
                "failed",
                failedAt,
                null,
                PaymentOperationTerminalDisposition.AttemptTerminal,
                PaymentOperationRetryDisposition.CreateNewAttempt,
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
            await createContext.SaveChangesAsync();
        }
        innerProvider.SetStatus(
            providerObjectId,
            providerStatus,
            string.Equals(providerStatus, "requires_capture", StringComparison.Ordinal)
                ? DateTimeOffset.UtcNow.AddDays(1)
                : null);
        var provider = new CountingStripeSessionClient(innerProvider);

        await using var retryContext = CreateContext();
        var retried = await Service(retryContext, provider).RetryAsync(
            specification.OperationId,
            attemptId,
            1);

        Assert.True(retried.TryGetError(out PaymentOperationError? error));
        Assert.Equal(expectedErrorType, error.GetType());
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, provider.CancellationCount);
        var attempts = await retryContext.PaymentSessionAttempts
            .Where(attempt => attempt.OperationId == specification.OperationId)
            .ToArrayAsync();
        var persistedAttempt = Assert.Single(attempts);
        Assert.Equal(PaymentOperationState.Failed, persistedAttempt.State);
        Assert.Equal("failed", persistedAttempt.LastProviderStatus);
        Assert.Equal(failedAt, persistedAttempt.LastObservedAt);
        Assert.Equal(failedAt, persistedAttempt.TerminalAt);
        Assert.Equal(PaymentOperationFailureCode.Declined, persistedAttempt.FailureCode);
    }

    [Fact]
    public async Task RetryAsync_ConcurrentDuplicateRetries_ConvergeAfterCancellationRace()
    {
        await MigrateAsync();
        var innerProvider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        Guid predecessorId;
        string predecessorProviderObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, innerProvider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? createdExecution));
            predecessorId = createdExecution.Identity.AttemptId;
            var attempt = await createContext.PaymentSessionAttempts.SingleAsync(value => value.AttemptId == predecessorId);
            predecessorProviderObjectId = attempt.ProviderObjectId!;
            attempt.ApplyTransition(new(
                PaymentOperationTransitionDisposition.Applied,
                PaymentOperationState.Failed,
                "failed",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                null,
                PaymentOperationTerminalDisposition.AttemptTerminal,
                PaymentOperationRetryDisposition.CreateNewAttempt,
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
            await createContext.SaveChangesAsync();
        }
        innerProvider.SetDeclined(predecessorProviderObjectId);
        var provider = new ConcurrentCancellationStripeSessionClient(innerProvider, predecessorProviderObjectId);

        async Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync()
        {
            await using var context = CreateContext();
            return await Service(context, provider).RetryAsync(specification.OperationId, predecessorId, 1);
        }

        var results = await Task.WhenAll(RetryAsync(), RetryAsync());

        Assert.All(results, result => Assert.True(result.TryGetValue(out _)));
        Assert.Equal(
            results[0].Match(value => value.Identity.AttemptId, _ => Guid.Empty),
            results[1].Match(value => value.Identity.AttemptId, _ => Guid.Empty));
        Assert.Equal(2, innerProvider.ProviderObjectCount);
        await using var assertContext = CreateContext();
        Assert.Equal(
            2,
            await assertContext.PaymentSessionAttempts.CountAsync(
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
        IStripeSessionClient provider) =>
        new(
            new PaymentSessionOperationRepository(context),
            new PaymentSessionAttemptRepository(context),
            new PayoutAccountRepository(context),
            provider,
            TimeProvider.System);

    private static PaymentSessionSpecification Specification(Guid operationId, long amountMinor = 5000) =>
        PaymentSessionSpecification.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"booking:{operationId:N}",
            $"payer:{operationId:N}",
            $"payee:{operationId:N}",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}");

    private sealed class CountingStripeSessionClient : IStripeSessionClient
    {
        private readonly IStripeSessionClient inner;

        public CountingStripeSessionClient(IStripeSessionClient inner)
        {
            this.inner = inner;
        }

        public int CallCount { get; private set; }
        public int CancellationCount { get; private set; }

        public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CreateAsync(
            PaymentSessionProviderRequest request,
            PaymentSessionIdempotencyKey idempotencyKey,
            CancellationToken ct = default)
        {
            CallCount++;
            return inner.CreateAsync(request, idempotencyKey, ct);
        }

        public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            CallCount++;
            return inner.RetrieveAsync(providerObjectKind, providerObjectId, ct);
        }

        public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CancelAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            CallCount++;
            CancellationCount++;
            return inner.CancelAsync(providerObjectKind, providerObjectId, ct);
        }

        public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
            string providerCustomerId,
            CancellationToken ct = default)
        {
            CallCount++;
            return inner.CreateCustomerSessionAsync(providerCustomerId, ct);
        }
    }

    private sealed class ConcurrentCancellationStripeSessionClient : IStripeSessionClient
    {
        private readonly FakeStripeSessionClient inner;
        private readonly string predecessorProviderObjectId;
        private readonly TaskCompletionSource retrievalsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int predecessorRetrievalCount;

        public ConcurrentCancellationStripeSessionClient(
            FakeStripeSessionClient inner,
            string predecessorProviderObjectId)
        {
            this.inner = inner;
            this.predecessorProviderObjectId = predecessorProviderObjectId;
        }

        public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CreateAsync(
            PaymentSessionProviderRequest request,
            PaymentSessionIdempotencyKey idempotencyKey,
            CancellationToken ct = default) =>
            inner.CreateAsync(request, idempotencyKey, ct);

        public async Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            var result = await inner.RetrieveAsync(providerObjectKind, providerObjectId, ct);
            if (!string.Equals(providerObjectId, predecessorProviderObjectId, StringComparison.Ordinal))
                return result;

            if (Interlocked.Increment(ref predecessorRetrievalCount) == 2)
                retrievalsCompleted.TrySetResult();

            await retrievalsCompleted.Task.WaitAsync(ct);
            return result;
        }

        public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CancelAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default) =>
            inner.CancelAsync(providerObjectKind, providerObjectId, ct);

        public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
            string providerCustomerId,
            CancellationToken ct = default) =>
            inner.CreateCustomerSessionAsync(providerCustomerId, ct);
    }
}
