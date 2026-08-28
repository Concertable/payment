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
    public async Task Webhook_UntrackedProviderObject_IsNoOpAndPreservesLegacyPublish()
    {
        await using var harness = await WebhookReconciliationHarness.CreateAsync(sql.ConnectionString);
        var providerObjectId = $"pi_untracked_{Guid.NewGuid():N}";

        await harness.ProcessWebhookAsync(PaymentIntentEvent(
            "evt_untracked",
            providerObjectId,
            "succeeded",
            EventTypes.PaymentIntentSucceeded));

        Assert.Equal(1, await harness.LegacyPaymentSucceededCountAsync(providerObjectId));
    }

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
}
