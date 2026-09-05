using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain;
using Concertable.Payment.IntegrationTests.Fixtures;
using Concertable.Testing.Integration;
using Stripe;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentSessionWebhookReconciliationTests : IClassFixture<SqlFixture>
{
    private static readonly DateTime EventCreated = new(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc);

    private readonly SqlFixture sql;

    public PaymentSessionWebhookReconciliationTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task Webhook_AfterProviderTransition_PublishesStateChangeOnce()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = Specification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        var eagerStateChanges = await harness.StateChangeCountAsync(specification.OperationId);
        harness.SessionClient.SetStatus(providerObjectId, "processing");

        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_transition", providerObjectId, "processing"));

        Assert.Equal(1, eagerStateChanges);
        Assert.Equal(2, await harness.StateChangeCountAsync(specification.OperationId));
        Assert.Equal(
            PaymentOperationState.Processing,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
    }

    [Fact]
    public async Task Webhook_DuplicateEventDelivery_PublishesStateChangeOnce()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = Specification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        harness.SessionClient.SetStatus(providerObjectId, "processing");

        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_dup", providerObjectId, "processing"));
        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_dup", providerObjectId, "processing"));

        Assert.Equal(2, await harness.StateChangeCountAsync(specification.OperationId));
        Assert.Equal(
            PaymentOperationState.Processing,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
    }

    [Fact]
    public async Task Webhook_ReorderedEventAfterTerminal_DoesNotRegressOrRepublish()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = Specification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        harness.SessionClient.SetStatus(providerObjectId, "succeeded");

        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_succeeded", providerObjectId, "succeeded"));
        var afterTerminal = await harness.StateChangeCountAsync(specification.OperationId);

        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_late", providerObjectId, "processing"));

        Assert.Equal(2, afterTerminal);
        Assert.Equal(afterTerminal, await harness.StateChangeCountAsync(specification.OperationId));
        Assert.Equal(
            PaymentOperationState.Succeeded,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
    }

    [Fact]
    public async Task Webhook_StalePayload_UsesRetrievedProviderTruth()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = Specification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        harness.SessionClient.SetStatus(providerObjectId, "succeeded");

        await harness.ProcessWebhookAsync(PaymentIntentEvent("evt_stale", providerObjectId, "processing"));

        Assert.Equal(
            PaymentOperationState.Succeeded,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
        Assert.Equal(2, await harness.StateChangeCountAsync(specification.OperationId));
    }

    [Fact]
    public async Task Webhook_UntrackedProviderObjectWithoutReference_IsNoOp()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var providerObjectId = $"pi_untracked_{Guid.NewGuid():N}";
        var before = await harness.PaymentSucceededCountAsync();

        await harness.ProcessWebhookAsync(PaymentIntentEvent(
            "evt_untracked",
            providerObjectId,
            "succeeded",
            EventTypes.PaymentIntentSucceeded));

        Assert.Equal(before, await harness.PaymentSucceededCountAsync());
    }

    [Fact]
    public async Task Webhook_SetupIntent_PublishesStateChangeOnce()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = SetupSpecification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        var eagerStateChanges = await harness.StateChangeCountAsync(specification.OperationId);
        harness.SessionClient.SetStatus(providerObjectId, "succeeded");

        await harness.ProcessWebhookAsync(SetupIntentEvent("evt_setup", providerObjectId, "succeeded"));

        Assert.Equal(1, eagerStateChanges);
        Assert.Equal(2, await harness.StateChangeCountAsync(specification.OperationId));
        Assert.Equal(
            PaymentOperationState.Succeeded,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
    }

    [Fact]
    public async Task Webhook_SetupIntentSucceeded_ForAVerification_PublishesPaymentSucceededOnce()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = VerificationSpecification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        harness.SessionClient.SetStatus(providerObjectId, "succeeded");

        await harness.ProcessWebhookAsync(SetupIntentEvent(
            "evt_verified",
            providerObjectId,
            "succeeded",
            EventTypes.SetupIntentSucceeded,
            harness.SessionClient.MetadataOf(providerObjectId)));

        Assert.Equal(1, await harness.PaymentSucceededCountAsync(specification.ClientReference));
        Assert.Equal(
            PaymentOperationState.Succeeded,
            (await harness.GetCurrentAttemptAsync(specification.OperationId)).State);
    }

    [Fact]
    public async Task Webhook_SetupIntentSucceeded_ForAMethodSetup_PublishesNothing()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var specification = SetupSpecification(Guid.CreateVersion7());
        await harness.CreateSessionAsync(specification);
        var providerObjectId = (await harness.GetCurrentAttemptAsync(specification.OperationId)).ProviderObjectId!;
        harness.SessionClient.SetStatus(providerObjectId, "succeeded");

        await harness.ProcessWebhookAsync(SetupIntentEvent(
            "evt_setup_done",
            providerObjectId,
            "succeeded",
            EventTypes.SetupIntentSucceeded,
            harness.SessionClient.MetadataOf(providerObjectId)));

        Assert.Equal(0, await harness.PaymentSucceededCountAsync(specification.ClientReference));
    }

    private static Event SetupIntentEvent(string eventId, string providerObjectId, string status) =>
        SetupIntentEvent(eventId, providerObjectId, status, "setup_intent.created", new Dictionary<string, string>());

    private static Event SetupIntentEvent(
        string eventId,
        string providerObjectId,
        string status,
        string type,
        IReadOnlyDictionary<string, string> metadata) =>
        new()
        {
            Id = eventId,
            Type = type,
            Created = EventCreated,
            Data = new EventData
            {
                Object = new SetupIntent
                {
                    Id = providerObjectId,
                    Status = status,
                    Metadata = metadata.ToDictionary(pair => pair.Key, pair => pair.Value),
                },
            },
        };

    private static PaymentSessionDefinition VerificationSpecification(Guid operationId) =>
        PaymentSessionDefinition.Create(
            operationId,
            PaymentSessionKind.PaymentMethodVerification,
            PaymentSession.OnSession,
            TransactionTypes.Verify,
            $"app:{operationId:N}",
            $"payer:{operationId:N}",
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            $"cus_{operationId:N}",
            null,
            "door-split-mandate-v1");

    private static PaymentSessionDefinition SetupSpecification(Guid operationId) =>
        PaymentSessionDefinition.Create(
            operationId,
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OffSession,
            "setup",
            $"setup:{operationId:N}",
            $"payer:{operationId:N}",
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            $"cus_{operationId:N}",
            null,
            "venue-hire-mandate-v1");

    private static Event PaymentIntentEvent(
        string eventId,
        string providerObjectId,
        string status,
        string type = "payment_intent.processing") =>
        new()
        {
            Id = eventId,
            Type = type,
            Created = EventCreated,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = providerObjectId,
                    Status = status,
                    Metadata = new Dictionary<string, string>(),
                },
            },
        };

    private static PaymentSessionDefinition Specification(Guid operationId, long amountMinor = 5000) =>
        PaymentSessionDefinition.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"order:{operationId:N}",
            $"payer:{operationId:N}",
            $"payee:{operationId:N}",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}",
            null);
}
