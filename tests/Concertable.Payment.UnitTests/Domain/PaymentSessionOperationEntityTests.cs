using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentSessionOperationEntityTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Authorization_ReservesRevisionOnePaymentIntent()
    {
        var operation = CreateOperation();

        Assert.Equal(1, operation.CurrentRevision);
        Assert.Equal(1, operation.Attempts.Count);
        Assert.Equal(PaymentSessionProviderObjectKind.PaymentIntent, operation.CurrentAttempt.ProviderObjectKind);
        Assert.Equal(PaymentOperationState.Creating, operation.CurrentAttempt.State);
        Assert.Null(operation.CurrentAttempt.PredecessorAttemptId);
    }

    [Fact]
    public void EvaluateInitialReservation_SameFingerprint_ReplaysCurrentAttempt()
    {
        var specification = Specification();
        var operation = PaymentSessionOperationEntity.Create(
            specification,
            Guid.CreateVersion7(CreatedAt),
            CreatedAt);

        var reservation = operation.EvaluateInitialReservation(
            PaymentSessionFingerprint.Create(specification));

        Assert.Equal(PaymentSessionReservationDisposition.Replayed, reservation.Disposition);
        Assert.Same(operation.CurrentAttempt, reservation.Attempt);
    }

    [Fact]
    public void EvaluateInitialReservation_ChangedFingerprint_ReturnsConflict()
    {
        var operation = CreateOperation();

        var reservation = operation.EvaluateInitialReservation(
            PaymentSessionFingerprint.Create(Specification(amountMinor: 5001)));

        Assert.Equal(PaymentSessionReservationDisposition.Conflict, reservation.Disposition);
        Assert.Null(reservation.Attempt);
    }

    [Fact]
    public void BindProviderObject_DifferentSecondBinding_ThrowsDomainException()
    {
        var attempt = CreateOperation().CurrentAttempt;
        attempt.BindProviderObject("pi_first");

        Assert.Throws<DomainException>(() => attempt.BindProviderObject("pi_second"));
    }

    [Fact]
    public void ReserveNextAttempt_EligibleFailure_CreatesMonotonicSuccessor()
    {
        var operation = CreateFailedOperation();
        var predecessor = operation.CurrentAttempt;

        var reservation = operation.ReserveNextAttempt(
            predecessor.AttemptId,
            predecessor.Revision,
            Guid.CreateVersion7(),
            CreatedAt.AddMinutes(1));

        Assert.Equal(PaymentSessionReservationDisposition.Created, reservation.Disposition);
        Assert.Equal(2, operation.CurrentRevision);
        Assert.Equal(2, operation.Attempts.Count);
        Assert.Equal(predecessor.AttemptId, reservation.Attempt!.PredecessorAttemptId);
        Assert.Equal(2, reservation.Attempt.Revision);
        Assert.Equal(PaymentOperationState.Creating, reservation.Attempt.State);
    }

    [Fact]
    public void ReserveNextAttempt_DuplicatePredecessor_ReplaysSuccessor()
    {
        var operation = CreateFailedOperation();
        var predecessor = operation.CurrentAttempt;
        var first = operation.ReserveNextAttempt(
            predecessor.AttemptId,
            predecessor.Revision,
            Guid.CreateVersion7(),
            CreatedAt.AddMinutes(1));

        var duplicate = operation.ReserveNextAttempt(
            predecessor.AttemptId,
            predecessor.Revision,
            Guid.CreateVersion7(),
            CreatedAt.AddMinutes(2));

        Assert.Equal(PaymentSessionReservationDisposition.Replayed, duplicate.Disposition);
        Assert.Same(first.Attempt, duplicate.Attempt);
        Assert.Equal(2, operation.Attempts.Count);
    }

    [Fact]
    public void ReserveNextAttempt_StaleRevision_ReturnsConflict()
    {
        var operation = CreateFailedOperation();

        var reservation = operation.ReserveNextAttempt(
            Guid.CreateVersion7(),
            2,
            Guid.NewGuid(),
            CreatedAt.AddMinutes(1));

        Assert.Equal(PaymentSessionReservationDisposition.Conflict, reservation.Disposition);
        Assert.Single(operation.Attempts);
    }

    private static PaymentSessionOperationEntity CreateOperation()
    {
        var specification = Specification();
        return PaymentSessionOperationEntity.Create(specification, Guid.CreateVersion7(CreatedAt), CreatedAt);
    }

    private static PaymentSessionOperationEntity CreateFailedOperation()
    {
        var operation = CreateOperation();
        operation.CurrentAttempt.BindProviderObject("pi_failed");
        operation.CurrentAttempt.ApplyTransition(operation.SessionKind, new(
            PaymentOperationState.Failed,
            "failed",
            CreatedAt.AddSeconds(1),
            null,
            PaymentOperationTerminalDisposition.AttemptTerminal,
            PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
        return operation;
    }

    private static PaymentSessionDefinition Specification(long amountMinor = 5000) =>
        PaymentSessionDefinition.Create(
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            PaymentSessionKind.Authorization,
            PaymentSession.OnSession,
            "escrow",
            "order:42",
            "payer:7",
            "payee:9",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            null,
            "cus_test",
            "acct_test",
            null);
}
