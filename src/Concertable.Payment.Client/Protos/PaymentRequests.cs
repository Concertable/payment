using Concertable.Payment.Client.Adapters;
using Concertable.Payment.Contracts;
using Google.Protobuf.WellKnownTypes;
using DomainMoney = Concertable.Kernel.ValueObjects.Money;
using DomainCurrency = Concertable.Kernel.ValueObjects.Currency;
using DomainDateRange = Concertable.Kernel.ValueObjects.DateRange;
using ContractPaymentMethodReference = Concertable.Payment.Contracts.PaymentOperationReference;

namespace Concertable.Payment.Grpc;

public sealed partial class SettlementPaymentRequest
{
    internal static SettlementPaymentRequest Create(
        Guid operationId,
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        ContractPaymentMethodReference paymentMethod,
        PaymentSession session)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));

        return new()
        {
            OperationId = operationId.ToString("D"),
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            PaymentMethod = PaymentRequestValidation.ToProto(paymentMethod),
            Session = session.ToProtoSession()
        };
    }
}

public sealed partial class BoundCommissionSettlementPaymentRequest
{
    internal static BoundCommissionSettlementPaymentRequest Create(
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        ContractPaymentMethodReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

        return new()
        {
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            PaymentMethod = PaymentRequestValidation.ToProto(paymentMethod),
            Session = session.ToProtoSession(),
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference
        };
    }
}

public sealed partial class RecentSettlementsRequest
{
    internal static RecentSettlementsRequest Create(Guid ownerId, int take)
    {
        PaymentRequestValidation.ThrowIfEmpty(ownerId, nameof(ownerId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return new()
        {
            OwnerId = ownerId.ToString("D"),
            Take = take
        };
    }
}

public sealed partial class PaymentPeriodRequest
{
    internal static PaymentPeriodRequest Create(Guid payeeId, DomainDateRange period)
    {
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));

        return new()
        {
            PayeeId = payeeId.ToString("D"),
            PeriodStart = Timestamp.FromDateTime(period.Start),
            PeriodEnd = Timestamp.FromDateTime(period.End)
        };
    }
}

public sealed partial class PreviewCommissionRequest
{
    internal static PreviewCommissionRequest Create(DomainMoney gross) =>
        new() { Gross = gross.ToProtoMoney() };
}

public sealed partial class CreateOrBindCommissionRequest
{
    internal static CreateOrBindCommissionRequest Create(
        string externalReference,
        string payerReference,
        DomainCurrency currency,
        Guid reviewedCommissionConfigurationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerReference);
        PaymentRequestValidation.ThrowIfEmpty(
            reviewedCommissionConfigurationId,
            nameof(reviewedCommissionConfigurationId));

        return new()
        {
            ExternalReference = externalReference,
            PayerReference = payerReference,
            Currency = currency.ToProtoCurrency(),
            ReviewedCommissionConfigurationId = reviewedCommissionConfigurationId.ToString("D")
        };
    }
}

public sealed partial class ConfirmReviewedGrossRequest
{
    internal static ConfirmReviewedGrossRequest Create(
        Guid bindingId,
        string externalReference,
        string payerReference,
        DomainMoney reviewedGross)
    {
        PaymentRequestValidation.ThrowIfEmpty(bindingId, nameof(bindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerReference);

        return new()
        {
            BindingId = bindingId.ToString("D"),
            ExternalReference = externalReference,
            PayerReference = payerReference,
            ReviewedGross = reviewedGross.ToProtoMoney()
        };
    }
}

public sealed partial class CalculateBoundCommissionRequest
{
    internal static CalculateBoundCommissionRequest Create(
        Guid bindingId,
        string externalReference,
        string payerReference,
        DomainMoney gross)
    {
        PaymentRequestValidation.ThrowIfEmpty(bindingId, nameof(bindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerReference);

        return new()
        {
            BindingId = bindingId.ToString("D"),
            ExternalReference = externalReference,
            PayerReference = payerReference,
            Gross = gross.ToProtoMoney()
        };
    }
}

public sealed partial class DepositRequest
{
    internal static DepositRequest Create(
        Guid operationId,
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        ContractPaymentMethodReference paymentMethod,
        PaymentSession session)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));

        return new()
        {
            OperationId = operationId.ToString("D"),
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            PaymentMethod = PaymentRequestValidation.ToProto(paymentMethod),
            Session = session.ToProtoSession()
        };
    }
}

public sealed partial class BoundCommissionDepositRequest
{
    internal static BoundCommissionDepositRequest Create(
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        ContractPaymentMethodReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

        return new()
        {
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            PaymentMethod = PaymentRequestValidation.ToProto(paymentMethod),
            Session = session.ToProtoSession(),
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference
        };
    }
}

public sealed partial class CaptureRequest
{
    internal static CaptureRequest Create(
        Guid operationId,
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        ContractPaymentMethodReference authorization)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));

        return new()
        {
            OperationId = operationId.ToString("D"),
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            Authorization = PaymentRequestValidation.ToProto(authorization)
        };
    }
}

public sealed partial class BoundCommissionCaptureRequest
{
    internal static BoundCommissionCaptureRequest Create(
        ContractPaymentMethodReference reference,
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        ContractPaymentMethodReference authorization,
        Guid commissionBindingId,
        string externalReference)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

        return new()
        {
            Reference = PaymentRequestValidation.ToProto(reference),
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            Authorization = PaymentRequestValidation.ToProto(authorization),
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference
        };
    }
}

public sealed partial class ReleaseEscrowRequest
{
    internal static ReleaseEscrowRequest Create(
        Guid operationId,
        ContractPaymentMethodReference reference)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));
        return new()
        {
            OperationId = operationId.ToString("D"),
            Reference = PaymentRequestValidation.ToProto(reference)
        };
    }
}

public sealed partial class RefundEscrowRequest
{
    internal static RefundEscrowRequest Create(
        Guid operationId,
        ContractPaymentMethodReference reference)
    {
        PaymentRequestValidation.ThrowIfEmpty(operationId, nameof(operationId));
        return new()
        {
            OperationId = operationId.ToString("D"),
            Reference = PaymentRequestValidation.ToProto(reference)
        };
    }
}

public sealed partial class BoundCommissionRefundRequest
{
    internal static BoundCommissionRefundRequest Create(
        ContractPaymentMethodReference reference,
        DomainMoney gross,
        string? reason = null) =>
        new()
        {
            Reference = PaymentRequestValidation.ToProto(reference),
            Gross = gross.ToProtoMoney(),
            Reason = reason ?? string.Empty
        };
}

public sealed partial class PayoutOwnerRequest
{
    internal static PayoutOwnerRequest Create(Guid ownerId)
    {
        PaymentRequestValidation.ThrowIfEmpty(ownerId, nameof(ownerId));
        return new() { OwnerId = ownerId.ToString("D") };
    }
}

internal static class PaymentRequestValidation
{
    public static void ThrowIfEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Value cannot be empty.", paramName);
    }

    public static PaymentOperationReference ToProto(ContractPaymentMethodReference reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.OperationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.ClientReference);
        return new()
        {
            OperationType = reference.OperationType,
            ClientReference = reference.ClientReference
        };
    }
}
