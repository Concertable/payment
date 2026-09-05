using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Grpc;
using Grpc.Core;
using Money = Concertable.Kernel.ValueObjects.Money;
using ContractReference = Concertable.Payment.Contracts.PaymentOperationReference;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record SettlementPaymentCommand(
    Guid OperationId,
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    ContractReference PaymentMethod,
    PaymentSession Session);

internal sealed record BoundCommissionSettlementPaymentCommand(
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    ContractReference PaymentMethod,
    PaymentSession Session,
    Guid CommissionBindingId,
    string ExternalReference);

internal sealed record PaymentPeriodCommand(Guid PayeeId, DateRange Period);

internal sealed record RecentSettlementsCommand(Guid OwnerId, int Take);

internal static class SettlementRequestMappers
{
    extension(SettlementPaymentRequest request)
    {
        public SettlementPaymentCommand ToCommand() => new(
            request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Amount.ToMoney(),
            request.PaymentMethod.ToContractReference(),
            request.Session.ToPaymentSession());
    }

    extension(BoundCommissionSettlementPaymentRequest request)
    {
        public BoundCommissionSettlementPaymentCommand ToCommand() => new(
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Gross.ToMoney(),
            request.PaymentMethod.ToContractReference(),
            request.Session.ToPaymentSession(),
            request.CommissionBindingId.ParseOrThrow<Guid>(nameof(request.CommissionBindingId)),
            request.ExternalReference);
    }

    extension(PaymentPeriodRequest request)
    {
        public PaymentPeriodCommand ToCommand()
        {
            if (request.PeriodStart is null || request.PeriodEnd is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period is required."));

            var start = request.PeriodStart.ToDateTimeOrThrow(nameof(request.PeriodStart));
            var end = request.PeriodEnd.ToDateTimeOrThrow(nameof(request.PeriodEnd));
            if (end <= start)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment period end must be after start."));

            return new(
                request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
                new DateRange(start, end));
        }
    }

    extension(RecentSettlementsRequest request)
    {
        public RecentSettlementsCommand ToCommand()
        {
            if (request.Take is < 1 or > 50)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Take must be between 1 and 50."));

            return new(
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
}
