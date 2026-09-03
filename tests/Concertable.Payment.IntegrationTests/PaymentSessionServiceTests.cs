using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.Lifecycle;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Payment.IntegrationTests.Fixtures;
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
    public async Task SetupPaymentMethodAsync_ReplayedReference_ReusesProviderObject()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerOwnerId = Guid.CreateVersion7();
        var reference = new PaymentOperationReference("applicationApply", $"application:{Guid.CreateVersion7():N}");
        await using (var seedContext = CreateContext())
            await SeedPayerAsync(seedContext, payerOwnerId);

        await using (var firstContext = CreateContext())
        {
            var first = await Service(firstContext, provider).SetupPaymentMethodAsync(
                new(reference, PaymentSessionKind.PaymentMethodSetup, payerOwnerId));

            Assert.True(first.TryGetValue(out _));
        }

        await using (var replayContext = CreateContext())
        {
            var replay = await Service(replayContext, provider).SetupPaymentMethodAsync(
                new(reference, PaymentSessionKind.PaymentMethodSetup, payerOwnerId));

            Assert.True(replay.TryGetValue(out _));
        }

        Assert.Equal(1, provider.ProviderObjectCount);
        await using var assertContext = CreateContext();
        Assert.Single(await assertContext.PaymentSessionOperations
            .Where(operation => operation.OperationType == reference.OperationType
                && operation.ConsumerCorrelation == reference.ConsumerCorrelation)
            .ToListAsync());
    }

    [Fact]
    public async Task ValidatePaymentMethodAsync_CompletedSetupForPayer_ReturnsSuccess()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerOwnerId = Guid.CreateVersion7();
        var reference = new PaymentOperationReference("applicationApply", $"application:{Guid.CreateVersion7():N}");
        Guid operationId;
        string providerObjectId;
        await using (var context = CreateContext())
        {
            await SeedPayerAsync(context, payerOwnerId);
            var service = Service(context, provider);
            var setup = await service.SetupPaymentMethodAsync(
                new(reference, PaymentSessionKind.PaymentMethodSetup, payerOwnerId));
            Assert.True(setup.TryGetValue(out _));
            var operation = await context.PaymentSessionOperations
                .Include(value => value.Attempts)
                .SingleAsync(value => value.OperationType == reference.OperationType
                    && value.ConsumerCorrelation == reference.ConsumerCorrelation);
            operationId = operation.OperationId;
            providerObjectId = operation.CurrentAttempt.ProviderObjectId!;
        }
        provider.SetStatus(providerObjectId, "succeeded");

        await using var validationContext = CreateContext();
        var validationService = Service(validationContext, provider);
        var refreshed = await validationService.RefreshAsync(operationId);
        var validated = await validationService.ValidatePaymentMethodAsync(new(reference, payerOwnerId));

        Assert.True(refreshed.TryGetValue(out _));
        Assert.True(validated.IsSuccess);
        Assert.Equal(
            $"pm_fake_{providerObjectId}",
            (await validationContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.OperationId == operationId)).PaymentMethodId);
    }

    [Fact]
    public async Task ValidatePaymentMethodAsync_DifferentPayer_ReturnsPaymentMethodRequired()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var payerOwnerId = Guid.CreateVersion7();
        var reference = new PaymentOperationReference("applicationApply", $"application:{Guid.CreateVersion7():N}");
        await using (var context = CreateContext())
        {
            await SeedPayerAsync(context, payerOwnerId);
            var setup = await Service(context, provider).SetupPaymentMethodAsync(
                new(reference, PaymentSessionKind.PaymentMethodSetup, payerOwnerId));
            Assert.True(setup.TryGetValue(out _));
        }

        await using var validationContext = CreateContext();
        var validated = await Service(validationContext, provider).ValidatePaymentMethodAsync(
            new(reference, Guid.CreateVersion7()));

        Assert.True(validated.TryGetError(out var error));
        Assert.IsType<PaymentOperationError.PaymentMethodRequired>(error);
    }

    [Fact]
    public async Task ResolveAuthorizationAsync_AuthorizedOperation_ReturnsProviderObjectInsidePayment()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var operationId = Guid.CreateVersion7();
        var payerOwnerId = Guid.CreateVersion7();
        var specification = PaymentSessionSpecification.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"booking:{operationId:N}",
            payerOwnerId.ToString("D"),
            Guid.CreateVersion7().ToString("D"),
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}");
        string providerObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out _));
            providerObjectId = (await createContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.OperationId == operationId)).ProviderObjectId!;
        }
        provider.SetStatus(providerObjectId, "requires_capture", DateTimeOffset.UtcNow.AddDays(1));

        await using var resolveContext = CreateContext();
        var operationRepository = new PaymentSessionOperationRepository(resolveContext);
        var refreshed = await Service(resolveContext, provider).RefreshAsync(operationId);
        var resolved = await new PaymentOperationResolver(operationRepository).ResolveAuthorizationAsync(
            new(specification.OperationType, specification.ConsumerCorrelation),
            payerOwnerId);

        Assert.True(refreshed.TryGetValue(out _));
        Assert.True(resolved.TryGetValue(out var resolvedProviderObjectId));
        Assert.Equal(providerObjectId, resolvedProviderObjectId);
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
    public async Task RefreshAsync_ProviderRetrievalUnavailable_PersistsReconciliationRequirement()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        PaymentOperationState initialState;
        DateTimeOffset? initialObservedAt;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out _));
            var attempt = await createContext.PaymentSessionAttempts
                .SingleAsync(value => value.OperationId == specification.OperationId);
            initialState = attempt.State;
            initialObservedAt = attempt.LastObservedAt;
        }

        await using var refreshContext = CreateContext();
        var refreshed = await Service(
            refreshContext,
            new UnavailableRetrievalStripeSessionClient(provider))
            .RefreshAsync(specification.OperationId);

        Assert.True(refreshed.TryGetError(out PaymentOperationError? error));
        Assert.IsType<PaymentOperationError.ProviderUnavailable>(error);
        var persisted = await refreshContext.PaymentSessionAttempts
            .SingleAsync(attempt => attempt.OperationId == specification.OperationId);
        Assert.Equal(initialState, persisted.State);
        Assert.Equal(initialObservedAt, persisted.LastObservedAt);
        Assert.NotNull(persisted.NextReconcileAt);
        Assert.Equal(persisted.LastAttemptedAt, persisted.NextReconcileAt);
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
            attempt.ApplyTransition(PaymentSessionKind.Authorization, new(
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
            attempt.ApplyTransition(PaymentSessionKind.Authorization, new(
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
            attempt.ApplyTransition(PaymentSessionKind.Authorization, new(
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
    public async Task ReconcileAsync_ConcurrentObservation_ConvergesOnOneAppliedTransition()
    {
        await MigrateAsync();
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var specification = Specification(Guid.CreateVersion7());
        string providerObjectId;
        await using (var createContext = CreateContext())
        {
            var created = await Service(createContext, provider).CreateOrReplayAsync(specification);
            Assert.True(created.TryGetValue(out PaymentSessionExecution? execution));
            providerObjectId = (await createContext.PaymentSessionAttempts
                .SingleAsync(attempt => attempt.AttemptId == execution.Identity.AttemptId)).ProviderObjectId!;
        }

        var retrieved = await provider.RetrieveAsync(
            PaymentSessionProviderObjectKind.PaymentIntent,
            providerObjectId);
        Assert.True(retrieved.TryGetValue(out var currentProvider));
        var observation = currentProvider with
        {
            Status = "processing",
            ObservedAt = currentProvider.ObservedAt.AddSeconds(1)
        };
        var savesMayProceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCount = 0;

        async Task<Result<PaymentSessionReconciliation, PaymentOperationError.ProviderUnavailable>>
            ReconcileAsync()
        {
            await using var context = CreateContext();
            var operation = await new PaymentSessionOperationRepository(context)
                .GetByOperationIdAsync(specification.OperationId);
            Assert.NotNull(operation);
            var repository = new PaymentSessionAttemptRepository(context);
            var unitOfWork = new CoordinatedUnitOfWork(
                new UnitOfWork(context),
                savesMayProceed,
                () => Interlocked.Increment(ref saveCount));
            var service = new PaymentSessionReconciliationService(repository, unitOfWork, new PaymentSessionStateMachine(), TimeProvider.System);
            return await service.ReconcileAsync(
                new(
                    operation,
                    operation.CurrentAttempt,
                    PaymentSessionReconciliationSource.Eager,
                    observation));
        }

        var results = await Task.WhenAll(ReconcileAsync(), ReconcileAsync());

        var reconciliations = results.Select(result =>
        {
            Assert.True(result.TryGetValue(out var reconciliation));
            return reconciliation;
        }).ToArray();
        Assert.All(reconciliations, reconciliation =>
            Assert.True(reconciliation.Evaluation.TryGetValue(out _)));
        await using var assertContext = CreateContext();
        var persisted = await assertContext.PaymentSessionAttempts
            .SingleAsync(attempt => attempt.OperationId == specification.OperationId);
        Assert.Equal(PaymentOperationState.Processing, persisted.State);
        Assert.Equal(observation.ObservedAt, persisted.LastObservedAt);
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

    private static async Task SeedPayerAsync(PaymentDbContext context, Guid payerOwnerId)
    {
        var payer = PayoutAccountEntity.Create(payerOwnerId, $"{payerOwnerId:N}@example.com");
        payer.LinkCustomer($"cus_{payerOwnerId:N}");
        context.PayoutAccounts.Add(payer);
        await context.SaveChangesAsync();
    }

    private static PaymentSessionService Service(
        PaymentDbContext context,
        IStripeSessionClient provider)
    {
        var attemptRepository = new PaymentSessionAttemptRepository(context);
        var operationRepository = new PaymentSessionOperationRepository(context);
        return new(
            operationRepository,
            new PayoutAccountRepository(context),
            new PaymentSessionReconciliationService(attemptRepository, new UnitOfWork(context), new PaymentSessionStateMachine(), TimeProvider.System),
            provider,
            new PaymentOperationResolver(operationRepository),
            TimeProvider.System);
    }

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
        private readonly IStripeSessionClient stripeSessionClient;

        public CountingStripeSessionClient(IStripeSessionClient stripeSessionClient)
        {
            this.stripeSessionClient = stripeSessionClient;
        }

        public int CallCount { get; private set; }
        public int CancellationCount { get; private set; }

        public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
            PaymentSessionProviderRequest request,
            PaymentSessionIdempotencyKey idempotencyKey,
            CancellationToken ct = default)
        {
            CallCount++;
            return stripeSessionClient.CreateAsync(request, idempotencyKey, ct);
        }

        public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            CallCount++;
            return stripeSessionClient.RetrieveAsync(providerObjectKind, providerObjectId, ct);
        }

        public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            CallCount++;
            CancellationCount++;
            return stripeSessionClient.CancelAsync(providerObjectKind, providerObjectId, ct);
        }

        public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
            string providerCustomerId,
            CancellationToken ct = default)
        {
            CallCount++;
            return stripeSessionClient.CreateCustomerSessionAsync(providerCustomerId, ct);
        }
    }

    private sealed class ConcurrentCancellationStripeSessionClient : IStripeSessionClient
    {
        private readonly FakeStripeSessionClient stripeSessionClient;
        private readonly string predecessorProviderObjectId;
        private readonly TaskCompletionSource retrievalsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int predecessorRetrievalCount;

        public ConcurrentCancellationStripeSessionClient(
            FakeStripeSessionClient stripeSessionClient,
            string predecessorProviderObjectId)
        {
            this.stripeSessionClient = stripeSessionClient;
            this.predecessorProviderObjectId = predecessorProviderObjectId;
        }

        public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
            PaymentSessionProviderRequest request,
            PaymentSessionIdempotencyKey idempotencyKey,
            CancellationToken ct = default) =>
            stripeSessionClient.CreateAsync(request, idempotencyKey, ct);

        public async Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default)
        {
            var result = await stripeSessionClient.RetrieveAsync(providerObjectKind, providerObjectId, ct);
            if (!string.Equals(providerObjectId, predecessorProviderObjectId, StringComparison.Ordinal))
                return result;

            if (Interlocked.Increment(ref predecessorRetrievalCount) == 2)
                retrievalsCompleted.TrySetResult();

            await retrievalsCompleted.Task.WaitAsync(ct);
            return result;
        }

        public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
            PaymentSessionProviderObjectKind providerObjectKind,
            string providerObjectId,
            CancellationToken ct = default) =>
            stripeSessionClient.CancelAsync(providerObjectKind, providerObjectId, ct);

        public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
            string providerCustomerId,
            CancellationToken ct = default) =>
            stripeSessionClient.CreateCustomerSessionAsync(providerCustomerId, ct);
    }
}
