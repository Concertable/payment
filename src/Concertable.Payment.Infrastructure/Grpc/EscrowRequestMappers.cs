using Concertable.Payment.Contracts;
using Concertable.Payment.Grpc;
using Grpc.Core;
using Money = Concertable.Kernel.ValueObjects.Money;
using ContractReference = Concertable.Payment.Contracts.PaymentOperationReference;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record DepositCommand(
    Guid OperationId,
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    ContractReference PaymentMethod,
    PaymentSession Session);

internal sealed record BoundCommissionDepositCommand(
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    ContractReference PaymentMethod,
    PaymentSession Session,
    Guid CommissionBindingId,
    string ExternalReference);

internal sealed record CaptureCommand(
    Guid OperationId,
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    ContractReference Authorization);

internal sealed record BoundCommissionCaptureCommand(
    ContractReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    ContractReference Authorization,
    Guid CommissionBindingId,
    string ExternalReference);

internal static class EscrowRequestMappers
{
    extension(DepositRequest request)
    {
        public DepositCommand ToCommand() => new(
            request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Amount.ToMoney(),
            request.PaymentMethod.ToContractReference(),
            request.Session.ToPaymentSession());
    }

    extension(BoundCommissionDepositRequest request)
    {
        public BoundCommissionDepositCommand ToCommand() => new(
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Gross.ToMoney(),
            request.PaymentMethod.ToContractReference(),
            request.Session.ToPaymentSession(),
            request.CommissionBindingId.ParseOrThrow<Guid>(nameof(request.CommissionBindingId)),
            request.ExternalReference);
    }

    extension(CaptureRequest request)
    {
        public CaptureCommand ToCommand() => new(
            request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Amount.ToMoney(),
            request.Authorization.ToContractReference());
    }

    extension(BoundCommissionCaptureRequest request)
    {
        public BoundCommissionCaptureCommand ToCommand() => new(
            request.Reference.ToContractReference(),
            request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
            request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
            request.Gross.ToMoney(),
            request.Authorization.ToContractReference(),
            request.CommissionBindingId.ParseOrThrow<Guid>(nameof(request.CommissionBindingId)),
            request.ExternalReference);
    }

    extension(Concertable.Payment.Grpc.PaymentOperationReference reference)
    {
        public ContractReference ToContractReference()
        {
            try
            {
                return new(reference.OperationType, reference.ClientReference);
            }
            catch (ArgumentException exception)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
            }
        }
    }
}
