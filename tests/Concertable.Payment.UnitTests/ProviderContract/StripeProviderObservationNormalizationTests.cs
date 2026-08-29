using System.Reflection;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain.ProviderContract;
using Stripe;

namespace Concertable.Payment.UnitTests.ProviderContract;

public sealed class StripeProviderObservationNormalizationTests
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
        Assert.Equal(StripeProviderContractBaseline.StripeNetVersion, informationalVersion.Split('+', 2)[0]);
        Assert.Equal("2025-01-27.acacia", StripeProviderContractBaseline.ApiVersion);
    }

    [Fact]
    public void StatusInventoriesMatchTheCompletePinnedVocabularies()
    {
        AssertStatuses(
            StripeProviderObjectKind.PaymentIntent,
            ["requires_payment_method", "requires_confirmation", "requires_action", "processing", "requires_capture", "canceled", "succeeded"]);
        AssertStatuses(
            StripeProviderObjectKind.SetupIntent,
            ["requires_payment_method", "requires_confirmation", "requires_action", "processing", "canceled", "succeeded"]);
        AssertStatuses(
            StripeProviderObjectKind.Refund,
            ["pending", "requires_action", "succeeded", "failed", "canceled"]);
    }

    [Theory]
    [InlineData("requires_payment_method", PaymentOperationState.RequiresPaymentMethod)]
    [InlineData("requires_confirmation", PaymentOperationState.RequiresConfirmation)]
    [InlineData("requires_action", PaymentOperationState.RequiresAction)]
    [InlineData("processing", PaymentOperationState.Processing)]
    [InlineData("succeeded", PaymentOperationState.Succeeded)]
    [InlineData("canceled", PaymentOperationState.Canceled)]
    public void PaymentIntentStatusesNormalize(string status, PaymentOperationState expected) =>
        Assert.Equal(
            expected,
            Normalize(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, status).State);

    [Fact]
    public void AuthorizationNormalizesRequiresCaptureWithDeadline()
    {
        var captureBefore = observedAt.AddDays(7);
        var observation = Normalize(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Authorization,
            "requires_capture",
            captureBefore);

        Assert.Equal(PaymentOperationState.Authorized, observation.State);
        Assert.Equal(captureBefore, observation.CaptureBefore);
    }

    [Theory]
    [InlineData("pending", PaymentOperationState.Processing)]
    [InlineData("requires_action", PaymentOperationState.RequiresAction)]
    [InlineData("succeeded", PaymentOperationState.Succeeded)]
    [InlineData("failed", PaymentOperationState.Failed)]
    [InlineData("canceled", PaymentOperationState.Canceled)]
    public void RefundStatusesNormalize(string status, PaymentOperationState expected) =>
        Assert.Equal(expected, Normalize(StripeProviderObjectKind.Refund, null, status).State);

    [Fact]
    public void UnknownStatusFailsClosed() =>
        Assert.Equal(
            PaymentOperationTransitionRejectionReason.UnknownProviderStatus,
            Reject(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, "future_status").Reason);

    [Fact]
    public void UnsupportedApiVersionIsRejected()
    {
        var observation = Observation(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment, "processing")
            with { ApiVersion = "future-version" };

        Assert.True(observation.ToNormalized(PaymentOperationState.Creating).TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.UnsupportedApiVersion, rejection.Reason);
    }

    [Fact]
    public void ProviderObjectMustMatchTheSessionProduct() =>
        Assert.Equal(
            PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
            Reject(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.PaymentMethodSetup, "processing").Reason);

    [Fact]
    public void ClassifiedDeclineOnRequiresPaymentMethodNormalizes()
    {
        var observation = Observation(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            "requires_payment_method") with { FailureClassification = ProviderFailureClassification.Declined };

        Assert.True(observation.ToNormalized(PaymentOperationState.Creating).TryGetValue(out var normalized));
        Assert.Equal(ProviderFailureClassification.Declined, normalized.FailureClassification);
    }

    [Fact]
    public void FailureClassificationOnAnyOtherStatusIsRejected()
    {
        var observation = Observation(
            StripeProviderObjectKind.PaymentIntent,
            PaymentSessionKind.Payment,
            "processing") with { FailureClassification = ProviderFailureClassification.Declined };

        Assert.True(observation.ToNormalized(PaymentOperationState.Creating).TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification, rejection.Reason);
    }

    private static PaymentProviderObservation Normalize(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        string status,
        DateTimeOffset? captureBefore = null)
    {
        var result = (Observation(providerObjectKind, sessionKind, status) with { CaptureBefore = captureBefore })
            .ToNormalized(PaymentOperationState.Creating);
        Assert.True(result.TryGetValue(out var normalized));
        return normalized;
    }

    private static PaymentOperationTransitionRejection Reject(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        string status)
    {
        var result = Observation(providerObjectKind, sessionKind, status).ToNormalized(PaymentOperationState.Creating);
        Assert.True(result.TryGetError(out var rejection));
        return rejection;
    }

    private static StripeProviderObservation Observation(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind,
        string status) =>
        new(
            StripeProviderContractBaseline.ApiVersion,
            providerObjectKind,
            "pi_test",
            operationId,
            attemptId,
            1,
            sessionKind,
            status,
            observedAt);

    private static void AssertStatuses(StripeProviderObjectKind providerObjectKind, string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            StripeProviderContractBaseline.NormalizedStates[providerObjectKind].Keys.Order(StringComparer.Ordinal));
}
