using Concertable.Payment.Client.Adapters;
using Concertable.Payment.Contracts;
using DomainMoney = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Grpc;

public sealed partial class ManagerPayRequest
{
    internal static ManagerPayRequest Create(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));

        var request = Create(payerId, payeeId, amount, paymentMethodId, session, bookingId);
        request.OperationId = operationId.ToString("D");
        return request;
    }

    internal static ManagerPayRequest Create(
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            PaymentMethodId = paymentMethodId,
            Session = session.ToProtoSession(),
            BookingId = bookingId
        };
    }
}

public sealed partial class ReleaseByBookingIdRequest
{
    internal static ReleaseByBookingIdRequest Create(Guid operationId, int bookingId)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));

        var request = Create(bookingId);
        request.OperationId = operationId.ToString("D");
        return request;
    }

    internal static ReleaseByBookingIdRequest Create(int bookingId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);
        return new() { BookingId = bookingId };
    }
}

file static class PaymentRequestValidation
{
    public static void ThrowIfEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Value cannot be empty.", paramName);
    }
}
