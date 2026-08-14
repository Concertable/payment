using Concertable.Payment.Grpc;
using Concertable.Kernel.ValueObjects;
using Grpc.Core;
using Money = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record ManagerPayCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record BoundCommissionManagerPayCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId,
    Guid CommissionBindingId,
    string ExternalReference,
    string? StripeSetupIntentId);

internal sealed record CreateSessionCommand(
    Guid PayerId,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed record CreateHoldSessionCommand(
    Guid PayerId,
    Money Amount,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed record CreateBoundCommissionHoldSessionCommand(
    Guid PayerId,
    Money Gross,
    IReadOnlyDictionary<string, string> Metadata,
    Guid CommissionBindingId,
    string ExternalReference,
    string? StripeSetupIntentId);

internal sealed record FindHeldIntentCommand(
    Guid PayerId,
    int ApplicationId);

internal sealed record PaymentPeriodCommand(Guid PayeeId, DateRange Period);

internal sealed record RecentSettlementsCommand(Guid OwnerId, int Take);

internal static class ManagerPaymentRequestMappers
{
    public static ManagerPayCommand ToCommand(this ManagerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static BoundCommissionManagerPayCommand ToCommand(
        this BoundCommissionManagerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Gross.ToMoney(),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId,
        request.CommissionBindingId.ParseOrThrow<Guid>(
            nameof(request.CommissionBindingId)),
        request.ExternalReference,
        EmptyToNull(request.StripeSetupIntentId));

    public static CreateSessionCommand ToCommand(this CreateSetupSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateSessionCommand ToCommand(this CreateVerifySessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateHoldSessionCommand ToCommand(this CreateHoldSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Amount.ToMoney(),
        request.Metadata);

    public static CreateBoundCommissionHoldSessionCommand ToCommand(
        this CreateBoundCommissionHoldSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Gross.ToMoney(),
        request.Metadata,
        request.CommissionBindingId.ParseOrThrow<Guid>(
            nameof(request.CommissionBindingId)),
        request.ExternalReference,
        EmptyToNull(request.StripeSetupIntentId));

    public static FindHeldIntentCommand ToCommand(this FindHeldIntentRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ApplicationId);

    public static PaymentPeriodCommand ToCommand(this PaymentPeriodRequest request) => new(
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.ToDateRange());

    public static RecentSettlementsCommand ToCommand(this RecentSettlementsRequest request)
    {
        if (request.Take is < 1 or > 50)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Take must be between 1 and 50."));

        return new RecentSettlementsCommand(
            request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)),
            request.Take);
    }

    private static DateRange ToDateRange(this PaymentPeriodRequest request)
    {
        if (request.PeriodStart is null || request.PeriodEnd is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period is required."));

        var start = request.PeriodStart.ToDateTimeOrThrow(nameof(request.PeriodStart));
        var end = request.PeriodEnd.ToDateTimeOrThrow(nameof(request.PeriodEnd));
        if (end <= start)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period end must be after start."));

        return new DateRange(start, end);
    }

    private static DateTime ToDateTimeOrThrow(this Google.Protobuf.WellKnownTypes.Timestamp timestamp, string fieldName)
    {
        try
        {
            return timestamp.ToDateTime();
        }
        catch (InvalidOperationException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} is not a valid timestamp."));
        }
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
