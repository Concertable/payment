namespace Concertable.Payment.Domain;

internal enum PaymentSessionReservationDisposition
{
    Created,
    Replayed,
    Conflict,
    NotFound,
    NotRetryable
}

internal sealed record PaymentSessionReservation(
    PaymentSessionReservationDisposition Disposition,
    PaymentSessionOperationEntity? Operation,
    PaymentSessionAttemptEntity? Attempt);
