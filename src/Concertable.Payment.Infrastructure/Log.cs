using Concertable.Payment.Domain;
using Microsoft.Extensions.Logging;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

namespace Concertable.Payment.Infrastructure;

internal static partial class Log
{
    #region WebhookProcessor

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing Stripe event {EventId} of type {EventType}")]
    internal static partial void ProcessingStripeEvent(this ILogger logger, string eventId, string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping Stripe event {EventId}: data object is {ObjectType}, not PaymentIntent")]
    internal static partial void SkippingStripeEventNotPaymentIntent(this ILogger logger, string eventId, string objectType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping Stripe event {EventId}: already processed")]
    internal static partial void SkippingStripeEventAlreadyProcessed(this ILogger logger, string eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publishing PaymentSucceededEvent for PaymentIntent {IntentId} (event {EventId}) of transaction type {TransactionType}")]
    internal static partial void PublishingPaymentSucceededEvent(this ILogger logger, string intentId, string eventId, string transactionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cancelling verify PaymentIntent {IntentId} after 3DS completion (event {EventId})")]
    internal static partial void CancellingVerifyPaymentIntent(this ILogger logger, string intentId, string eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publishing PaymentSucceededEvent for verify PaymentIntent {IntentId} (event {EventId})")]
    internal static partial void PublishingVerifyPaymentSucceededEvent(this ILogger logger, string intentId, string eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publishing PaymentFailedEvent for PaymentIntent {IntentId} (event {EventId}) of transaction type {TransactionType}: {Code} {Message}")]
    internal static partial void PublishingPaymentFailedEvent(this ILogger logger, string intentId, string eventId, string transactionType, string? code, string? message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping Stripe event {EventId}: type {EventType} not handled")]
    internal static partial void SkippingStripeEventNotHandled(this ILogger logger, string eventId, string eventType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing Stripe webhook for event {EventId}")]
    internal static partial void StripeWebhookProcessingError(this ILogger logger, string eventId, Exception ex);

    #endregion

    #region StripeTransferClient

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe escrow release {TransferId} succeeded: {AmountPence} pence to {Destination} from charge {ChargeId}")]
    internal static partial void StripeEscrowReleaseSucceeded(this ILogger logger, string transferId, long amountPence, string destination, string chargeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe release failed for {AmountPence} pence to {Destination} from charge {ChargeId}: {StripeCode}")]
    internal static partial void StripeReleaseFailed(this ILogger logger, long amountPence, string destination, string chargeId, string? stripeCode, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Release processing failed for {AmountPence} pence to {Destination}")]
    internal static partial void ReleaseProcessingFailed(this ILogger logger, long amountPence, string destination, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe transfer reversal succeeded for transfer {TransferId}: {AmountPence} pence")]
    internal static partial void StripeTransferReversalSucceeded(this ILogger logger, string transferId, long amountPence);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe refund {RefundId} succeeded for payment intent {IntentId}: {AmountPence} pence")]
    internal static partial void StripeRefundSucceeded(this ILogger logger, string refundId, string intentId, long amountPence);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe refund failed for payment intent {IntentId}: {StripeCode}")]
    internal static partial void StripeRefundFailed(this ILogger logger, string intentId, string? stripeCode, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Refund processing failed for payment intent {IntentId}")]
    internal static partial void RefundProcessingFailed(this ILogger logger, string intentId, Exception ex);

    #endregion

    #region StripePaymentIntentClient

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe payment intent {IntentId} succeeded: {AmountPence} pence to {Destination}")]
    internal static partial void StripePaymentIntentSucceeded(this ILogger logger, string intentId, long amountPence, string destination);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe payment intent {IntentId} returned non-succeeded status {Status}: {AmountPence} pence to {Destination}")]
    internal static partial void StripePaymentIntentNonSucceeded(this ILogger logger, string intentId, string status, long amountPence, string destination);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe charge failed for {AmountPence} pence to {Destination}: {StripeCode}")]
    internal static partial void StripeChargeFailed(this ILogger logger, long amountPence, string destination, string? stripeCode, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment processing failed for {AmountPence} pence to {Destination}")]
    internal static partial void PaymentProcessingFailed(this ILogger logger, long amountPence, string destination, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stripe escrow hold {IntentId} succeeded: {AmountPence} pence held in platform balance on behalf of {Destination}")]
    internal static partial void StripeEscrowHoldSucceeded(this ILogger logger, string intentId, long amountPence, string destination);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe escrow hold {IntentId} returned non-succeeded status {Status}: {AmountPence} pence on behalf of {Destination}")]
    internal static partial void StripeEscrowHoldNonSucceeded(this ILogger logger, string intentId, string status, long amountPence, string destination);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe hold failed for {AmountPence} pence on behalf of {Destination}: {StripeCode}")]
    internal static partial void StripeHoldFailed(this ILogger logger, long amountPence, string destination, string? stripeCode, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hold processing failed for {AmountPence} pence on behalf of {Destination}")]
    internal static partial void HoldProcessingFailed(this ILogger logger, long amountPence, string destination, Exception ex);

    #endregion

    #region PaymentManager

    [LoggerMessage(Level = LogLevel.Information, Message = "Charging {PayerId} {Amount} GBP -> {PayeeId} (stripe {DestinationStripeId}) for {Purpose}")]
    internal static partial void ChargingPayment(this ILogger logger, Guid payerId, decimal amount, Guid payeeId, string destinationStripeId, string purpose);

    [LoggerMessage(Level = LogLevel.Information, Message = "Holding {Amount} GBP from {PayerId} on behalf of {PayeeId} (stripe {DestinationStripeId}) for {Purpose}")]
    internal static partial void HoldingPayment(this ILogger logger, decimal amount, Guid payerId, Guid payeeId, string destinationStripeId, string purpose);

    [LoggerMessage(Level = LogLevel.Information, Message = "Releasing {Amount} GBP from platform balance to {PayeeId} (stripe {DestinationStripeId}) from charge {ChargeId}")]
    internal static partial void ReleasingPayment(this ILogger logger, decimal amount, Guid payeeId, string destinationStripeId, string chargeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refunding {Amount} GBP for payment intent {IntentId}{TransferReversal}")]
    internal static partial void RefundingPayment(this ILogger logger, decimal amount, string intentId, string transferReversal);

    [LoggerMessage(Level = LogLevel.Information, Message = "Capturing PaymentIntent {PaymentIntentId} for {Purpose}")]
    internal static partial void CapturingPaymentIntent(this ILogger logger, string paymentIntentId, string purpose);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe capture failed for PaymentIntent {PaymentIntentId}: {StripeCode}")]
    internal static partial void StripeCaptureFailedForPaymentIntent(this ILogger logger, string paymentIntentId, string? stripeCode, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Capture failed for PaymentIntent {PaymentIntentId}")]
    internal static partial void CaptureFailedForPaymentIntent(this ILogger logger, string paymentIntentId, Exception ex);

    #endregion

    #region TransactionHandlerFactory

    [LoggerMessage(Level = LogLevel.Error, Message = "No ITransactionHandler is registered for transaction type {TransactionType}. Check ServiceCollectionExtensions for AddKeyedScoped registrations.")]
    internal static partial void NoTransactionHandlerRegistered(this ILogger logger, string transactionType);

    #endregion

    #region SettlementFailedHandler

    [LoggerMessage(Level = LogLevel.Warning, Message = "No settlement transaction found for charge {ChargeId}; ignoring PaymentFailedEvent")]
    internal static partial void NoSettlementTransactionFound(this ILogger logger, string chargeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settlement transaction {TransactionId} already in status {Status}; skipping fail")]
    internal static partial void SettlementTransactionAlreadyInStatus(this ILogger logger, int transactionId, TransactionStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settlement transaction {TransactionId} failed (Pending -> Failed) for charge {ChargeId}: {Code} {Message}")]
    internal static partial void SettlementTransactionFailed(this ILogger logger, int transactionId, string chargeId, string? code, string? message);

    #endregion

    #region PaymentTransactionHandler

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching PaymentSucceededEvent for transaction {TransactionId} to handler for type {TransactionType}")]
    internal static partial void DispatchingPaymentSucceededEvent(this ILogger logger, string transactionId, string transactionType);

    #endregion

    #region PaymentFailureDispatcher

    [LoggerMessage(Level = LogLevel.Warning, Message = "No IPaymentFailureHandler registered for transaction type {TransactionType}; ignoring failure for {TransactionId}")]
    internal static partial void NoPaymentFailureHandlerRegistered(this ILogger logger, string transactionType, string transactionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching PaymentFailedEvent for transaction {TransactionId} (code {Code}) to handler for type {TransactionType}")]
    internal static partial void DispatchingPaymentFailedEvent(this ILogger logger, string transactionId, string? code, string transactionType);

    #endregion

    #region EscrowConfirmedHandler

    [LoggerMessage(Level = LogLevel.Warning, Message = "No escrow found for charge {ChargeId}; ignoring PaymentSucceededEvent")]
    internal static partial void NoEscrowFoundForPaymentSucceeded(this ILogger logger, string chargeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Escrow {EscrowId} already in status {Status}; skipping confirm")]
    internal static partial void EscrowAlreadyConfirmedStatus(this ILogger logger, int escrowId, EscrowStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Escrow {EscrowId} confirmed (Pending -> Held) for charge {ChargeId}")]
    internal static partial void EscrowConfirmed(this ILogger logger, int escrowId, string chargeId);

    #endregion

    #region EscrowFailedHandler

    [LoggerMessage(Level = LogLevel.Warning, Message = "No escrow found for charge {ChargeId}; ignoring PaymentFailedEvent")]
    internal static partial void NoEscrowFoundForPaymentFailed(this ILogger logger, string chargeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Escrow {EscrowId} already in status {Status}; skipping fail")]
    internal static partial void EscrowAlreadyFailedStatus(this ILogger logger, int escrowId, EscrowStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Escrow {EscrowId} failed (Pending -> Failed) for charge {ChargeId}: {Code} {Message}")]
    internal static partial void EscrowFailed(this ILogger logger, int escrowId, string chargeId, string? code, string? message);

    #endregion

    #region EscrowService

    [LoggerMessage(Level = LogLevel.Warning, Message = "No escrow found for booking {BookingId}; nothing to release")]
    internal static partial void NoEscrowFoundForBooking(this ILogger logger, int bookingId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Escrow {EscrowId} for booking {BookingId} is {Status}, not Held; skipping release")]
    internal static partial void EscrowNotHeldSkippingRelease(this ILogger logger, int escrowId, int bookingId, EscrowStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No escrow found for booking {BookingId}; nothing to refund")]
    internal static partial void NoEscrowToRefundForBooking(this ILogger logger, int bookingId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Escrow {EscrowId} for booking {BookingId} already Refunded; skipping refund")]
    internal static partial void EscrowAlreadyRefunded(this ILogger logger, int escrowId, int bookingId);

    #endregion

    #region GrpcExceptionInterceptor

    [LoggerMessage(Level = LogLevel.Warning, Message = "gRPC handler returned error in {Method}: {StatusCode} {Detail}")]
    internal static partial void GrpcHandlerRpcError(this ILogger logger, string method, GrpcStatusCode statusCode, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "gRPC handler error in {Method}")]
    internal static partial void GrpcHandlerError(this ILogger logger, string method, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception in gRPC handler {Method}")]
    internal static partial void GrpcHandlerUnhandledException(this ILogger logger, string method, Exception ex);

    #endregion
}
