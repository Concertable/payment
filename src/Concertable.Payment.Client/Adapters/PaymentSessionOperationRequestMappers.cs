using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentSessionOperationRequestMappers
{
    extension(PaymentMethodSetupRequest request)
    {
        public Proto.PaymentMethodSetupRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.PayerOwnerId, nameof(request.PayerOwnerId));
            ArgumentException.ThrowIfNullOrWhiteSpace(request.MandateTermsVersion);

            return new()
            {
                Reference = request.Reference.ToProto(),
                Kind = request.Kind.ToProto(),
                PayerOwnerId = request.PayerOwnerId.ToString("D"),
                MandateTermsVersion = request.MandateTermsVersion
            };
        }
    }

    extension(PaymentMethodValidationRequest request)
    {
        public Proto.PaymentMethodValidationRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.PayerOwnerId, nameof(request.PayerOwnerId));

            return new()
            {
                Reference = request.Reference.ToProto(),
                PayerOwnerId = request.PayerOwnerId.ToString("D")
            };
        }
    }

    extension(PaymentOperationReference reference)
    {
        private Proto.PaymentOperationReference ToProto()
        {
            reference = reference.EnsureValid();

            return new()
            {
                OperationType = reference.OperationType,
                ClientReference = reference.ClientReference
            };
        }
    }

    extension(PaymentSessionOperationRequest request)
    {
        public Proto.PaymentSessionOperationRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OperationId, nameof(request.OperationId));
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.PayerOwnerId, nameof(request.PayerOwnerId));
            var reference = request.Reference.EnsureValid();

            var message = new Proto.PaymentSessionOperationRequest
            {
                OperationId = request.OperationId.ToString("D"),
                Kind = request.Kind.ToProto(),
                Session = request.Session.ToProto(),
                OperationType = reference.OperationType,
                ClientReference = reference.ClientReference,
                PayerOwnerId = request.PayerOwnerId.ToString("D"),
                FundsRouting = request.FundsRouting.ToProto()
            };
            if (request.PayeeOwnerId is { } payeeOwnerId)
                message.PayeeOwnerId = payeeOwnerId.ToString("D");
            if (request.AmountMinor is { } amountMinor)
                message.AmountMinor = amountMinor;
            if (request.Currency is { } currency)
                message.Currency = currency.ToProtoCurrency();
            if (request.MandateTermsVersion is { } mandateTermsVersion)
                message.MandateTermsVersion = mandateTermsVersion;

            return message;
        }
    }

    extension(PaymentSession session)
    {
        private Proto.PaymentSessionType ToProto() => session switch
        {
            PaymentSession.OnSession => Proto.PaymentSessionType.OnSession,
            PaymentSession.OffSession => Proto.PaymentSessionType.OffSession,
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, null)
        };
    }

    extension(PaymentSessionRetryRequest request)
    {
        public Proto.PaymentSessionRetryRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OperationId, nameof(request.OperationId));
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.ExpectedAttemptId, nameof(request.ExpectedAttemptId));
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OwnerId, nameof(request.OwnerId));

            return new()
            {
                OperationId = request.OperationId.ToString("D"),
                ExpectedAttemptId = request.ExpectedAttemptId.ToString("D"),
                ExpectedRevision = request.ExpectedRevision,
                OwnerId = request.OwnerId.ToString("D")
            };
        }
    }

    extension(PaymentSessionStatusRequest request)
    {
        public Proto.PaymentSessionStatusRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OperationId, nameof(request.OperationId));
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OwnerId, nameof(request.OwnerId));

            return new()
            {
                OperationId = request.OperationId.ToString("D"),
                OwnerId = request.OwnerId.ToString("D")
            };
        }
    }

    extension(PaymentSessionKind kind)
    {
        private Proto.PaymentSessionKind ToProto() => kind switch
        {
            PaymentSessionKind.Payment => Proto.PaymentSessionKind.Payment,
            PaymentSessionKind.Authorization => Proto.PaymentSessionKind.Authorization,
            PaymentSessionKind.PaymentMethodSetup => Proto.PaymentSessionKind.PaymentMethodSetup,
            PaymentSessionKind.PaymentMethodVerification => Proto.PaymentSessionKind.PaymentMethodVerification,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    extension(PaymentSessionFundsRouting fundsRouting)
    {
        private Proto.PaymentSessionFundsRouting ToProto() => fundsRouting switch
        {
            PaymentSessionFundsRouting.None => Proto.PaymentSessionFundsRouting.None,
            PaymentSessionFundsRouting.Platform => Proto.PaymentSessionFundsRouting.Platform,
            PaymentSessionFundsRouting.Destination => Proto.PaymentSessionFundsRouting.Destination,
            _ => throw new ArgumentOutOfRangeException(nameof(fundsRouting), fundsRouting, null)
        };
    }
}
