using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentSessionOperationRequestMappers
{
    extension(PaymentSessionOperationRequest request)
    {
        public Proto.PaymentSessionOperationRequest ToProto()
        {
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.OperationId, nameof(request.OperationId));
            Proto.PaymentRequestValidation.ThrowIfEmpty(request.PayerOwnerId, nameof(request.PayerOwnerId));
            ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationType);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ConsumerCorrelation);

            var message = new Proto.PaymentSessionOperationRequest
            {
                OperationId = request.OperationId.ToString("D"),
                Kind = request.Kind.ToProto(),
                Session = request.Session.ToProto(),
                OperationType = request.OperationType,
                ConsumerCorrelation = request.ConsumerCorrelation,
                PayerOwnerId = request.PayerOwnerId.ToString("D"),
                FundsRouting = request.FundsRouting.ToProto()
            };
            if (request.PayeeOwnerId is { } payeeOwnerId)
                message.PayeeOwnerId = payeeOwnerId.ToString("D");
            if (request.AmountMinor is { } amountMinor)
                message.AmountMinor = amountMinor;
            if (request.Currency is { } currency)
                message.Currency = currency.ToProtoCurrency();
            if (request.PaymentMethodId is { } paymentMethodId)
                message.PaymentMethodId = paymentMethodId;

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
