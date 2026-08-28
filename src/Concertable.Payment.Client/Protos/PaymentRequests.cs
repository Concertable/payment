using Concertable.Payment.Client.Adapters;
using Concertable.Payment.Contracts;
using Google.Protobuf.WellKnownTypes;
using DomainMoney = Concertable.Kernel.ValueObjects.Money;
using DomainCurrency = Concertable.Kernel.ValueObjects.Currency;
using DomainDateRange = Concertable.Kernel.ValueObjects.DateRange;

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

public sealed partial class BoundCommissionManagerPayRequest
{
    internal static BoundCommissionManagerPayRequest Create(
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            PaymentMethodId = paymentMethodId,
            Session = session.ToProtoSession(),
            BookingId = bookingId,
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference,
            StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
        };
    }
}

public sealed partial class CreateSetupSessionRequest
{
    internal static CreateSetupSessionRequest Create(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));

        var request = new CreateSetupSessionRequest { PayerId = payerId.ToString("D") };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
    }
}

public sealed partial class CreateVerifySessionRequest
{
    internal static CreateVerifySessionRequest Create(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));

        var request = new CreateVerifySessionRequest { PayerId = payerId.ToString("D") };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
    }
}

public sealed partial class CreateHoldSessionRequest
{
    internal static CreateHoldSessionRequest Create(
        Guid payerId,
        DomainMoney amount,
        IReadOnlyDictionary<string, string> metadata)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));

        var request = new CreateHoldSessionRequest
        {
            PayerId = payerId.ToString("D"),
            Amount = amount.ToProtoMoney()
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
    }
}

public sealed partial class CreateBoundCommissionHoldSessionRequest
{
    internal static CreateBoundCommissionHoldSessionRequest Create(
        Guid payerId,
        DomainMoney gross,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

        var request = new CreateBoundCommissionHoldSessionRequest
        {
            PayerId = payerId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference,
            StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
    }
}

public sealed partial class FindHeldIntentRequest
{
    internal static FindHeldIntentRequest Create(Guid payerId, int applicationId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(applicationId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            ApplicationId = applicationId
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

public sealed partial class CustomerPayRequest
{
    internal static CustomerPayRequest Create(
        Guid payerId,
        int concertId,
        Guid payeeId,
        DomainMoney amount,
        string paymentMethodId,
        IReadOnlyDictionary<string, string> metadata)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concertId);

        var request = new CustomerPayRequest
        {
            PayerId = payerId.ToString("D"),
            ConcertId = concertId,
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            PaymentMethodId = paymentMethodId
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
    }
}

public sealed partial class CreatePaymentSessionRequest
{
    internal static CreatePaymentSessionRequest Create(
        Guid payerId,
        int concertId,
        Guid payeeId,
        IReadOnlyDictionary<string, string> metadata)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concertId);

        var request = new CreatePaymentSessionRequest
        {
            PayerId = payerId.ToString("D"),
            ConcertId = concertId,
            PayeeId = payeeId.ToString("D")
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return request;
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
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId)
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
            ReviewedCommissionConfigurationId = reviewedCommissionConfigurationId.ToString("D"),
            StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
            StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
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
        DomainMoney gross,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId)
    {
        PaymentRequestValidation.ThrowIfEmpty(bindingId, nameof(bindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerReference);

        return new()
        {
            BindingId = bindingId.ToString("D"),
            ExternalReference = externalReference,
            PayerReference = payerReference,
            Gross = gross.ToProtoMoney(),
            StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
            StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
        };
    }
}

public sealed partial class DepositRequest
{
    internal static DepositRequest Create(
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

public sealed partial class BoundCommissionDepositRequest
{
    internal static BoundCommissionDepositRequest Create(
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            PaymentMethodId = paymentMethodId,
            Session = session.ToProtoSession(),
            BookingId = bookingId,
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference,
            StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
        };
    }
}

public sealed partial class CaptureRequest
{
    internal static CaptureRequest Create(
        Guid payerId,
        Guid payeeId,
        DomainMoney amount,
        string paymentIntentId,
        int bookingId)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Amount = amount.ToProtoMoney(),
            PaymentIntentId = paymentIntentId,
            BookingId = bookingId
        };
    }
}

public sealed partial class BoundCommissionCaptureRequest
{
    internal static BoundCommissionCaptureRequest Create(
        Guid payerId,
        Guid payeeId,
        DomainMoney gross,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference)
    {
        PaymentRequestValidation.ThrowIfEmpty(payerId, nameof(payerId));
        PaymentRequestValidation.ThrowIfEmpty(payeeId, nameof(payeeId));
        PaymentRequestValidation.ThrowIfEmpty(commissionBindingId, nameof(commissionBindingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);

        return new()
        {
            PayerId = payerId.ToString("D"),
            PayeeId = payeeId.ToString("D"),
            Gross = gross.ToProtoMoney(),
            PaymentIntentId = paymentIntentId,
            BookingId = bookingId,
            CommissionBindingId = commissionBindingId.ToString("D"),
            ExternalReference = externalReference
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

public sealed partial class RefundByBookingIdRequest
{
    internal static RefundByBookingIdRequest Create(int bookingId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);
        return new() { BookingId = bookingId };
    }
}

public sealed partial class BoundCommissionRefundByBookingIdRequest
{
    internal static BoundCommissionRefundByBookingIdRequest Create(int bookingId, DomainMoney gross)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookingId);
        return new() { BookingId = bookingId, Gross = gross.ToProtoMoney() };
    }
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
}
