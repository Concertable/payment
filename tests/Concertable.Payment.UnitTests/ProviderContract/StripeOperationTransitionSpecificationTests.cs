using System.Reflection;
using Concertable.Payment.Domain.ProviderContract;
using Stripe;

namespace Concertable.Payment.UnitTests.ProviderContract;

public sealed class StripeOperationTransitionSpecificationTests
{
    private static readonly Guid operationId = Guid.Parse("019c1234-0000-7000-8000-000000000001");
    private static readonly Guid attemptId = Guid.Parse("019c1234-0000-7000-8000-000000000002");
    private static readonly DateTimeOffset observedAt = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BaselinePinsStripeNetAndApiVersions()
    {
        var informationalVersion = typeof(StripeClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        Assert.NotNull(informationalVersion);
        Assert.Equal(
            StripeProviderContractBaseline.StripeNetVersion,
            informationalVersion.Split('+', 2)[0]);
        Assert.Equal("2025-01-27.acacia", StripeProviderContractBaseline.ApiVersion);
    }

    [Fact]
    public void StatusInventoriesMatchTheCompletePinnedVocabularies()
    {
        Assert.Equal(
            [
                "requires_payment_method",
                "requires_confirmation",
                "requires_action",
                "processing",
                "requires_capture",
                "canceled",
                "succeeded"
            ],
            StripeProviderContractBaseline.PaymentIntentStatuses);
        Assert.Equal(
            [
                "requires_payment_method",
                "requires_confirmation",
                "requires_action",
                "processing",
                "canceled",
                "succeeded"
            ],
            StripeProviderContractBaseline.SetupIntentStatuses);
        Assert.Equal(
            ["pending", "requires_action", "succeeded", "failed", "canceled"],
            StripeProviderContractBaseline.RefundStatuses);
    }

    [Theory]
    [InlineData("requires_payment_method", PaymentOperationState.RequiresPaymentMethod)]
    [InlineData("requires_confirmation", PaymentOperationState.RequiresConfirmation)]
    [InlineData("requires_action", PaymentOperationState.RequiresAction)]
    [InlineData("processing", PaymentOperationState.Processing)]
    [InlineData("succeeded", PaymentOperationState.Succeeded)]
    [InlineData("canceled", PaymentOperationState.Canceled)]
    public void PaymentIntentStatusesNormalize(string status, PaymentOperationState expected)
    {
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, status));

        Assert.Equal(expected, transition.State);
    }

    [Fact]
    public void AuthorizationNormalizesRequiresCaptureAndRequiresCaptureBefore()
    {
        var captureBefore = observedAt.AddDays(7);
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Authorization,
                "requires_capture",
                captureBefore: captureBefore));

        Assert.Equal(PaymentOperationState.Authorized, transition.State);
        Assert.Equal(captureBefore, transition.CaptureBefore);
        Assert.Equal(PaymentOperationTerminalDisposition.NonTerminal, transition.TerminalDisposition);
    }

    [Theory]
    [InlineData("requires_payment_method", PaymentOperationState.RequiresPaymentMethod)]
    [InlineData("requires_confirmation", PaymentOperationState.RequiresConfirmation)]
    [InlineData("requires_action", PaymentOperationState.RequiresAction)]
    [InlineData("processing", PaymentOperationState.Processing)]
    [InlineData("succeeded", PaymentOperationState.Succeeded)]
    [InlineData("canceled", PaymentOperationState.Canceled)]
    public void SetupIntentStatusesNormalize(string status, PaymentOperationState expected)
    {
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup),
            Observation(StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup, status));

        Assert.Equal(expected, transition.State);
    }

    [Theory]
    [InlineData("pending", PaymentOperationState.Processing)]
    [InlineData("requires_action", PaymentOperationState.RequiresAction)]
    [InlineData("succeeded", PaymentOperationState.Succeeded)]
    [InlineData("failed", PaymentOperationState.Failed)]
    [InlineData("canceled", PaymentOperationState.Canceled)]
    public void RefundStatusesNormalize(string status, PaymentOperationState expected)
    {
        var currentState = expected is PaymentOperationState.Succeeded
            or PaymentOperationState.Canceled
            or PaymentOperationState.Failed
                ? PaymentOperationState.Processing
                : PaymentOperationState.Creating;
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.Refund, null, currentState),
            Observation(StripeProviderObjectKind.Refund, null, status));

        Assert.Equal(expected, transition.State);
    }

    [Fact]
    public void StatePairTableIsExhaustive()
    {
        foreach (var pair in AllStatePairs())
        {
            Assert.Equal(
                pair.Expected,
                StripeOperationTransitionSpecification.IsAllowedSameRevisionTransition(
                    pair.Current,
                    pair.Next,
                    pair.ProviderObjectKind,
                    pair.SessionKind));
        }
    }

    [Fact]
    public void UnknownStatusesFailClosed()
    {
        var specifications = new (StripeProviderObjectKind ProviderObjectKind, PaymentSessionKind? SessionKind)[]
        {
            (StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            (StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup),
            (StripeProviderObjectKind.Refund, null)
        };

        foreach (var specification in specifications)
        {
            var rejection = EvaluateRejection(
                Attempt(specification.ProviderObjectKind, specification.SessionKind),
                Observation(specification.ProviderObjectKind, specification.SessionKind, "future_status"));

            Assert.Equal(PaymentOperationTransitionRejectionReason.UnknownProviderStatus, rejection.Reason);
        }
    }

    [Fact]
    public void RequiresCaptureFailsForAutomaticPayment()
    {
        var rejection = EvaluateRejection(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "requires_capture",
                captureBefore: observedAt.AddDays(7)));

        Assert.Equal(
            PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
            rejection.Reason);
    }

    [Fact]
    public void RequiresCaptureFailsWithoutCaptureDeadline()
    {
        var rejection = EvaluateRejection(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization),
            Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization, "requires_capture"));

        Assert.Equal(PaymentOperationTransitionRejectionReason.CaptureDeadlineRequired, rejection.Reason);
    }

    [Fact]
    public void IllegalSameRevisionEdgeIsRejected()
    {
        var rejection = EvaluateRejection(
            Attempt(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                PaymentOperationState.Processing),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "requires_confirmation"));

        Assert.Equal(PaymentOperationTransitionRejectionReason.IllegalTransition, rejection.Reason);
    }

    [Fact]
    public void ProviderObjectMustMatchTheSessionProduct()
    {
        var rejection = EvaluateRejection(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.PaymentMethodSetup),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.PaymentMethodSetup,
                "processing"));

        Assert.Equal(
            PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
            rejection.Reason);
    }

    [Fact]
    public void PersistedStateMustBeValidForTheProviderProduct()
    {
        var rejection = EvaluateRejection(
            Attempt(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                PaymentOperationState.Authorized),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "processing"));

        Assert.Equal(
            PaymentOperationTransitionRejectionReason.InvalidCurrentStateForProviderObject,
            rejection.Reason);
    }

    [Fact]
    public void ObservationMustMatchPersistedIdentity()
    {
        var reasons = new[]
        {
            PaymentOperationTransitionRejectionReason.UnsupportedApiVersion,
            PaymentOperationTransitionRejectionReason.OperationMismatch,
            PaymentOperationTransitionRejectionReason.AttemptMismatch,
            PaymentOperationTransitionRejectionReason.StaleRevision,
            PaymentOperationTransitionRejectionReason.FutureRevision,
            PaymentOperationTransitionRejectionReason.ProviderObjectMismatch,
            PaymentOperationTransitionRejectionReason.SessionKindMismatch
        };

        foreach (var reason in reasons)
        {
            var current = Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment);
            var observation = Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, "processing");
            observation = reason switch
            {
                PaymentOperationTransitionRejectionReason.UnsupportedApiVersion =>
                    observation with { ApiVersion = "future-version" },
                PaymentOperationTransitionRejectionReason.OperationMismatch =>
                    observation with { OperationId = Guid.NewGuid() },
                PaymentOperationTransitionRejectionReason.AttemptMismatch =>
                    observation with { AttemptId = Guid.NewGuid() },
                PaymentOperationTransitionRejectionReason.StaleRevision => observation with { Revision = 0 },
                PaymentOperationTransitionRejectionReason.FutureRevision => observation with { Revision = 2 },
                PaymentOperationTransitionRejectionReason.ProviderObjectMismatch =>
                    observation with { ProviderObjectId = "pi_other" },
                PaymentOperationTransitionRejectionReason.SessionKindMismatch =>
                    observation with { SessionKind = PaymentSessionKind.Authorization },
                _ => throw new ArgumentOutOfRangeException(nameof(reason))
            };

            Assert.Equal(reason, EvaluateRejection(current, observation).Reason);
        }
    }

    [Fact]
    public void SameStatusAndStateIsADuplicate()
    {
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            PaymentOperationState.Processing,
            "processing",
            observedAt);

        var transition = EvaluateSuccess(
            current,
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "processing",
                observedAt));

        Assert.Equal(PaymentOperationTransitionDisposition.Duplicate, transition.Disposition);
    }

    [Fact]
    public void SameStateDeclineIsAppliedWhenThePersistedFailureChanges()
    {
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            PaymentOperationState.RequiresPaymentMethod,
            "requires_payment_method",
            observedAt) with
        {
            Failure = new PaymentOperationFailure(
                PaymentOperationFailureCode.PaymentMethodRequired,
                "A usable payment method is required.")
        };
        var observation = Observation(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            "requires_payment_method",
            observedAt) with
        {
            FailureClassification = ProviderFailureClassification.Declined
        };

        var transition = EvaluateSuccess(current, observation);

        Assert.Equal(PaymentOperationTransitionDisposition.Applied, transition.Disposition);
        Assert.Equal(PaymentOperationFailureCode.Declined, transition.Failure?.Code);
        Assert.Equal("The payment was declined.", transition.Failure?.Message);
    }

    [Fact]
    public void SameStateAuthorizationIsAppliedWhenTheCaptureDeadlineChanges()
    {
        var originalCaptureBefore = observedAt.AddDays(7);
        var revisedCaptureBefore = originalCaptureBefore.AddHours(-1);
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Authorization,
            PaymentOperationState.Authorized,
            "requires_capture",
            observedAt) with
        {
            CaptureBefore = originalCaptureBefore
        };

        var transition = EvaluateSuccess(
            current,
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Authorization,
                "requires_capture",
                observedAt,
                revisedCaptureBefore));

        Assert.Equal(PaymentOperationTransitionDisposition.Applied, transition.Disposition);
        Assert.Equal(revisedCaptureBefore, transition.CaptureBefore);
    }

    [Fact]
    public void OlderObservationIsRejected()
    {
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            PaymentOperationState.Processing,
            "processing",
            observedAt);

        var rejection = EvaluateRejection(
            current,
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "requires_action",
                observedAt.AddSeconds(-1)));

        Assert.Equal(PaymentOperationTransitionRejectionReason.StaleObservation, rejection.Reason);
    }

    [Fact]
    public void DifferentStatusAtSameTimestampIsRejectedAsAmbiguous()
    {
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            PaymentOperationState.Processing,
            "processing",
            observedAt);

        var rejection = EvaluateRejection(
            current,
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "requires_action",
                observedAt));

        Assert.Equal(PaymentOperationTransitionRejectionReason.AmbiguousObservationOrder, rejection.Reason);
    }

    [Fact]
    public void TerminalAttemptRejectsALaterRegression()
    {
        var current = Attempt(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            PaymentOperationState.Succeeded,
            "succeeded",
            observedAt);

        var rejection = EvaluateRejection(
            current,
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "processing",
                observedAt.AddSeconds(1)));

        Assert.Equal(PaymentOperationTransitionRejectionReason.TerminalStateProtected, rejection.Reason);
    }

    [Fact]
    public void ExplicitConsumerCancellationTerminatesTheOperation()
    {
        var current = Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment);
        var observation = Observation(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            "canceled") with
        {
            IsExplicitConsumerCancellation = true
        };

        var transition = EvaluateSuccess(current, observation);

        Assert.Equal(PaymentOperationTerminalDisposition.OperationTerminal, transition.TerminalDisposition);
        Assert.Equal(PaymentOperationRetryDisposition.NotRetryable, transition.RetryDisposition);
    }

    [Fact]
    public void ProviderCancellationTerminatesOnlyTheAttempt()
    {
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, "canceled"));

        Assert.Equal(PaymentOperationTerminalDisposition.AttemptTerminal, transition.TerminalDisposition);
    }

    [Theory]
    [InlineData("requires_payment_method", PaymentOperationFailureCode.PaymentMethodRequired, "A usable payment method is required.")]
    [InlineData("requires_action", PaymentOperationFailureCode.AuthenticationRequired, "Payment authentication is required.")]
    [InlineData("canceled", PaymentOperationFailureCode.Canceled, "The payment operation was canceled.")]
    public void PublicFailuresAreClosedAndProviderSafe(
        string status,
        PaymentOperationFailureCode code,
        string message)
    {
        var transition = EvaluateSuccess(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, status));

        Assert.Equal(code, transition.Failure?.Code);
        Assert.Equal(message, transition.Failure?.Message);
        Assert.DoesNotContain("pi_", transition.Failure?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("_secret_", transition.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifiedDeclinesPreserveRecoverableStateAndEmitSafeFailure()
    {
        Assert.Equal([ProviderFailureClassification.Declined], Enum.GetValues<ProviderFailureClassification>());

        var specifications = new (StripeProviderObjectKind ProviderObjectKind, PaymentSessionKind SessionKind)[]
        {
            (StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            (StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization),
            (StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup),
            (StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodVerification)
        };

        foreach (var specification in specifications)
        {
            var transition = EvaluateSuccess(
                Attempt(specification.ProviderObjectKind, specification.SessionKind),
                Observation(specification.ProviderObjectKind, specification.SessionKind, "requires_payment_method") with
                {
                    FailureClassification = ProviderFailureClassification.Declined
                });

            Assert.Equal(PaymentOperationState.RequiresPaymentMethod, transition.State);
            Assert.Equal(PaymentOperationTerminalDisposition.NonTerminal, transition.TerminalDisposition);
            Assert.Equal(PaymentOperationRetryDisposition.RetryCurrentAttempt, transition.RetryDisposition);
            Assert.Equal(PaymentOperationFailureCode.Declined, transition.Failure?.Code);
            Assert.Equal("The payment was declined.", transition.Failure?.Message);
        }
    }

    [Fact]
    public void FailureClassificationsAreRejectedForEveryOtherProviderStatus()
    {
        var observations = StripeProviderContractBaseline.PaymentIntentStatuses
            .Where(status => status != "requires_payment_method")
            .Select(status => (
                ProviderObjectKind: StripeProviderObjectKind.PaymentIntent,
                SessionKind: (PaymentSessionKind?)(status == "requires_capture"
                    ? PaymentSessionKind.Authorization
                    : PaymentSessionKind.Payment),
                Status: status,
                CaptureBefore: status == "requires_capture" ? observedAt.AddDays(7) : (DateTimeOffset?)null))
            .Concat(StripeProviderContractBaseline.SetupIntentStatuses
                .Where(status => status != "requires_payment_method")
                .Select(status => (
                    ProviderObjectKind: StripeProviderObjectKind.SetupIntent,
                    SessionKind: (PaymentSessionKind?)PaymentSessionKind.PaymentMethodSetup,
                    Status: status,
                    CaptureBefore: (DateTimeOffset?)null)))
            .Concat(StripeProviderContractBaseline.RefundStatuses
                .Select(status => (
                    ProviderObjectKind: StripeProviderObjectKind.Refund,
                    SessionKind: (PaymentSessionKind?)null,
                    Status: status,
                    CaptureBefore: (DateTimeOffset?)null)));

        foreach (var observation in observations)
            foreach (var classification in Enum.GetValues<ProviderFailureClassification>())
            {
                var rejection = EvaluateRejection(
                    Attempt(observation.ProviderObjectKind, observation.SessionKind),
                    Observation(
                        observation.ProviderObjectKind,
                        observation.SessionKind,
                        observation.Status,
                        captureBefore: observation.CaptureBefore) with
                    {
                        FailureClassification = classification
                    });

                Assert.Equal(
                    PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification,
                    rejection.Reason);
            }
    }

    [Fact]
    public void UnknownFailureClassificationFailsClosed()
    {
        var rejection = EvaluateRejection(
            Attempt(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            Observation(
                StripeProviderObjectKind.PaymentIntent,
                PaymentSessionKind.Payment,
                "requires_payment_method") with
            {
                FailureClassification = (ProviderFailureClassification)int.MaxValue
            });

        Assert.Equal(
            PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification,
            rejection.Reason);
    }

    private static IEnumerable<(
        StripeProviderObjectKind ProviderObjectKind,
        PaymentSessionKind? SessionKind,
        PaymentOperationState Current,
        PaymentOperationState Next,
        bool Expected)> AllStatePairs()
    {
        var specifications = new (StripeProviderObjectKind ProviderObjectKind, PaymentSessionKind? SessionKind)[]
        {
            (StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment),
            (StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization),
            (StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup),
            (StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodVerification),
            (StripeProviderObjectKind.Refund, null)
        };

        foreach (var specification in specifications)
            foreach (var current in Enum.GetValues<PaymentOperationState>())
                foreach (var next in Enum.GetValues<PaymentOperationState>())
                {
                    yield return (
                        specification.ProviderObjectKind,
                        specification.SessionKind,
                        current,
                        next,
                        ExpectedEdge(specification.ProviderObjectKind, specification.SessionKind, current, next));
                }
    }

    private static bool ExpectedEdge(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        PaymentOperationState current,
        PaymentOperationState next)
    {
        var validStates = providerObjectKind switch
        {
            StripeProviderObjectKind.PaymentIntent when sessionKind == PaymentSessionKind.Authorization =>
                Enum.GetValues<PaymentOperationState>().ToHashSet(),
            StripeProviderObjectKind.PaymentIntent or StripeProviderObjectKind.SetupIntent =>
                Enum.GetValues<PaymentOperationState>()
                    .Where(state => state != PaymentOperationState.Authorized)
                    .ToHashSet(),
            StripeProviderObjectKind.Refund => new HashSet<PaymentOperationState>
            {
                PaymentOperationState.Creating,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            },
            _ => []
        };

        if (!validStates.Contains(current) || !validStates.Contains(next))
            return false;

        if (current == next)
            return true;

        return providerObjectKind == StripeProviderObjectKind.Refund
            ? RefundEdges.Contains((current, next))
            : PaymentEdges.Contains((current, next));
    }

    private static readonly HashSet<(PaymentOperationState Current, PaymentOperationState Next)> PaymentEdges =
    [
        .. EdgesFrom(PaymentOperationState.Creating,
            PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing,
            PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing,
            PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing,
            PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.RequiresAction,
            PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.Processing,
            PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.Processing,
            PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresAction,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.Authorized,
            PaymentOperationState.Processing,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled)
    ];

    private static readonly HashSet<(PaymentOperationState Current, PaymentOperationState Next)> RefundEdges =
    [
        .. EdgesFrom(PaymentOperationState.Creating,
            PaymentOperationState.Processing,
            PaymentOperationState.RequiresAction),
        .. EdgesFrom(PaymentOperationState.Processing,
            PaymentOperationState.RequiresAction,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed),
        .. EdgesFrom(PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing,
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed)
    ];

    private static IEnumerable<(PaymentOperationState Current, PaymentOperationState Next)> EdgesFrom(
        PaymentOperationState current,
        params PaymentOperationState[] nextStates) =>
        nextStates.Select(next => (current, next));

    private static PaymentProviderAttempt Attempt(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        PaymentOperationState state = PaymentOperationState.Creating,
        string? lastProviderStatus = null,
        DateTimeOffset? lastObservedAt = null) =>
        new(
            operationId,
            attemptId,
            1,
            providerObjectKind,
            ProviderObjectId(providerObjectKind),
            sessionKind,
            state,
            "fingerprint-v1",
            lastProviderStatus,
            lastObservedAt);

    private static StripeProviderObservation Observation(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        string status,
        DateTimeOffset? at = null,
        DateTimeOffset? captureBefore = null) =>
        new(
            StripeProviderContractBaseline.ApiVersion,
            providerObjectKind,
            ProviderObjectId(providerObjectKind),
            operationId,
            attemptId,
            1,
            sessionKind,
            status,
            at ?? observedAt,
            captureBefore);

    private static string ProviderObjectId(StripeProviderObjectKind providerObjectKind) =>
        providerObjectKind switch
        {
            StripeProviderObjectKind.PaymentIntent => "pi_test",
            StripeProviderObjectKind.SetupIntent => "seti_test",
            StripeProviderObjectKind.Refund => "re_test",
            _ => throw new ArgumentOutOfRangeException(nameof(providerObjectKind))
        };

    private static PaymentOperationTransition EvaluateSuccess(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        var result = StripeOperationTransitionSpecification.Evaluate(current, observation);
        Assert.True(result.TryGetValue(out var transition));
        return transition;
    }

    private static PaymentOperationTransitionRejection EvaluateRejection(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        var result = StripeOperationTransitionSpecification.Evaluate(current, observation);
        Assert.True(result.TryGetError(out var rejection));
        return rejection;
    }
}
