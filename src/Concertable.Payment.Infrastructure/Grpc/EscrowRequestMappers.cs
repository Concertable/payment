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
    public static DepositCommand ToCommand(this DepositRequest request) => new(
        request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
        request.Reference.ToContractReference(),
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentMethod.ToContractReference(),
        request.Session.ToPaymentSession());

    public static BoundCommissionDepositCommand ToCommand(
        this BoundCommissionDepositRequest request) => new(
        request.Reference.ToContractReference(),
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Gross.ToMoney(),
        request.PaymentMethod.ToContractReference(),
        request.Session.ToPaymentSession(),
        request.CommissionBindingId.ParseOrThrow<Guid>(nameof(request.CommissionBindingId)),
        request.ExternalReference);

    public static CaptureCommand ToCommand(this CaptureRequest request) => new(
        request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
        request.Reference.ToContractReference(),
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.Authorization.ToContractReference());

    public static BoundCommissionCaptureCommand ToCommand(
        this BoundCommissionCaptureRequest request) => new(
        request.Reference.ToContractReference(),
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Gross.ToMoney(),
        request.Authorization.ToContractReference(),
        request.CommissionBindingId.ParseOrThrow<Guid>(nameof(request.CommissionBindingId)),
        request.ExternalReference);

    public static ContractReference ToContractReference(
        this Concertable.Payment.Grpc.PaymentOperationReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.OperationType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment operation type is required."));
        if (string.IsNullOrWhiteSpace(reference.ClientReference))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Payment client reference is required."));

        return new(reference.OperationType, reference.ClientReference);
    }
}
