using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class SettlementOperationsGrpcService : SettlementOperations.SettlementOperationsBase
{
    private readonly ISettlementService settlementService;

    public SettlementOperationsGrpcService(ISettlementService settlementService)
    {
        this.settlementService = settlementService;
    }

    public override async Task<PaymentResponse> Pay(
        SettlementPaymentRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await settlementService.PayAsync(
            command.OperationId,
            command.Reference,
            command.PayerId,
            command.PayeeId,
            command.Amount,
            command.PaymentMethod,
            command.Session,
            context.CancellationToken);
        return result.ValueOrRpcException().ToProtoPaymentResponse();
    }

    public override async Task<PaymentResponse> PayBoundCommission(
        BoundCommissionSettlementPaymentRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await settlementService.PayBoundCommissionAsync(
            command.Reference,
            command.PayerId,
            command.PayeeId,
            command.Gross,
            command.PaymentMethod,
            command.Session,
            command.CommissionBindingId,
            command.ExternalReference,
            context.CancellationToken);
        return result.ValueOrRpcException().ToProtoPaymentResponse();
    }

    public override async Task<RefundResponse> RefundBoundCommission(
        BoundCommissionRefundRequest request,
        ServerCallContext context)
    {
        var result = await settlementService.RefundBoundCommissionAsync(
            request.Reference.ToContractReference(),
            request.Gross.ToMoney(),
            string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason,
            ct: context.CancellationToken);
        var refund = result.ValueOrRpcException();
        return refund.TryGetValue(out var value)
            ? new RefundResponse { Id = value.Id.ToString("D") }
            : new RefundResponse();
    }
}
