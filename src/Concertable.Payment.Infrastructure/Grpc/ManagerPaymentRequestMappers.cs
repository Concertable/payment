using Concertable.Payment.Grpc;
using Concertable.Kernel.ValueObjects;
using Grpc.Core;
using Money = Concertable.Kernel.ValueObjects.Money;
using ContractPaymentMethodReference = Concertable.Payment.Contracts.PaymentOperationReference;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record ManagerPayCommand(
    Guid? OperationId,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record ManagerPayUsingPaymentMethodCommand(
    Guid OperationId,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    ContractPaymentMethodReference PaymentMethod,
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
    extension(ManagerPayUsingPaymentMethodRequest request)
    {
        public ManagerPayUsingPaymentMethodCommand ToCommand() => new(
            request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Amount.ToMoney(),
            new(
                request.PaymentMethod.OperationType,
                request.PaymentMethod.ConsumerCorrelation),
            request.Session.ToPaymentSession(),
            request.BookingId);
    }

    extension(ManagerPayRequest request)
    {
        public ManagerPayCommand ToCommand() => new(
            ParseOptionalGuid(request.OperationId, nameof(request.OperationId)),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Amount.ToMoney(),
            request.PaymentMethodId,
            request.Session.ToPaymentSession(),
            request.BookingId);
    }

    extension(BoundCommissionManagerPayRequest request)
    {
        public BoundCommissionManagerPayCommand ToCommand() => new(
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
    }

    extension(CreateSetupSessionRequest request)
    {
        public CreateSessionCommand ToCommand() => new(
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.Metadata);
    }

    extension(CreateVerifySessionRequest request)
    {
        public CreateSessionCommand ToCommand() => new(
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.Metadata);
    }

    extension(CreateHoldSessionRequest request)
    {
        public CreateHoldSessionCommand ToCommand() => new(
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.Amount.ToMoney(),
            request.Metadata);
    }

    extension(CreateBoundCommissionHoldSessionRequest request)
    {
        public CreateBoundCommissionHoldSessionCommand ToCommand() => new(
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.Gross.ToMoney(),
            request.Metadata,
            request.CommissionBindingId.ParseOrThrow<Guid>(
                nameof(request.CommissionBindingId)),
            request.ExternalReference,
            EmptyToNull(request.StripeSetupIntentId));
    }

    extension(FindHeldIntentRequest request)
    {
        public FindHeldIntentCommand ToCommand() => new(
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.ApplicationId);
    }

    extension(PaymentPeriodRequest request)
    {
        public PaymentPeriodCommand ToCommand() => new(
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.ToDateRange());

        private DateRange ToDateRange()
        {
            if (request.PeriodStart is null || request.PeriodEnd is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period is required."));

            var start = request.PeriodStart.ToDateTimeOrThrow(nameof(request.PeriodStart));
            var end = request.PeriodEnd.ToDateTimeOrThrow(nameof(request.PeriodEnd));
            if (end <= start)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period end must be after start."));

            return new DateRange(start, end);
        }
    }

    extension(RecentSettlementsRequest request)
    {
        public RecentSettlementsCommand ToCommand()
        {
            if (request.Take is < 1 or > 50)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Take must be between 1 and 50."));

            return new RecentSettlementsCommand(
                request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)),
                request.Take);
        }
    }

    extension(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
    {
        private DateTime ToDateTimeOrThrow(string fieldName)
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
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static Guid? ParseOptionalGuid(string value, string fieldName) =>
        string.IsNullOrEmpty(value)
            ? null
            : value.ParseOrThrow<Guid>(fieldName);
}
