namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderOperationContextExtensions
{
    extension(PaymentProviderOperationContext context)
    {
        internal bool SupportsState(PaymentOperationState state) =>
            context switch
            {
                PaymentProviderOperationContext.Payment => state != PaymentOperationState.Authorized,
                PaymentProviderOperationContext.Authorization => true,
                PaymentProviderOperationContext.PaymentMethodSetup => state != PaymentOperationState.Authorized,
                PaymentProviderOperationContext.PaymentMethodVerification => state != PaymentOperationState.Authorized,
                PaymentProviderOperationContext.Refund => state is PaymentOperationState.Creating
                    or PaymentOperationState.RequiresAction
                    or PaymentOperationState.Processing
                    or PaymentOperationState.Succeeded
                    or PaymentOperationState.Canceled
                    or PaymentOperationState.Failed,
                _ => false
            };

        internal bool HasSameProviderProductAs(PaymentProviderOperationContext other) =>
            (context, other) switch
            {
                (PaymentProviderOperationContext.Payment or PaymentProviderOperationContext.Authorization,
                    PaymentProviderOperationContext.Payment or PaymentProviderOperationContext.Authorization) => true,
                (PaymentProviderOperationContext.PaymentMethodSetup
                    or PaymentProviderOperationContext.PaymentMethodVerification,
                    PaymentProviderOperationContext.PaymentMethodSetup
                    or PaymentProviderOperationContext.PaymentMethodVerification) => true,
                (PaymentProviderOperationContext.Refund, PaymentProviderOperationContext.Refund) => true,
                _ => false
            };
    }
}
