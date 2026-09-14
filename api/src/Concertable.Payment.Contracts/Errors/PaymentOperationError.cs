using System.Collections.Frozen;
using Dunet;
using Reunion.Errors;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentOperationError : IError
{
    private static readonly FrozenDictionary<PaymentOperationFailureCode, PaymentOperationError> byCode =
        new Dictionary<PaymentOperationFailureCode, PaymentOperationError>
        {
            [PaymentOperationFailureCode.PaymentMethodRequired] = new PaymentMethodRequired(),
            [PaymentOperationFailureCode.AuthenticationRequired] = new AuthenticationRequired(),
            [PaymentOperationFailureCode.Declined] = new Declined(),
            [PaymentOperationFailureCode.Expired] = new Expired(),
            [PaymentOperationFailureCode.Canceled] = new Canceled(),
            [PaymentOperationFailureCode.OperationConflict] = new OperationConflict(),
            [PaymentOperationFailureCode.ProviderUnavailable] = new ProviderUnavailable(),
            [PaymentOperationFailureCode.Unknown] = new Unknown()
        }.ToFrozenDictionary();

    public static PaymentOperationError FromCode(PaymentOperationFailureCode code) =>
        byCode.TryGetValue(code, out var error)
            ? error
            : throw new ArgumentOutOfRangeException(nameof(code), code, null);

    public ErrorDefinition Definition => this switch
    {
        PaymentMethodRequired => ErrorDefinition.PaymentRequired<PaymentMethodRequired>("A usable payment method is required."),
        AuthenticationRequired => ErrorDefinition.PaymentRequired<AuthenticationRequired>("Payment authentication is required."),
        Declined => ErrorDefinition.PaymentRequired<Declined>("The payment was declined."),
        Expired => ErrorDefinition.Conflict<Expired>("The payment attempt expired."),
        Canceled => ErrorDefinition.Conflict<Canceled>("The payment operation was canceled."),
        OperationConflict => ErrorDefinition.Conflict<OperationConflict>("The operation identity conflicts with an existing payment operation."),
        ProviderUnavailable => ErrorDefinition.Conflict<ProviderUnavailable>("The payment provider state is temporarily unavailable."),
        Unknown => ErrorDefinition.Conflict<Unknown>("The payment state could not be safely classified.")
    };

    [ErrorCode("payment.operation.payment_method_required")]
    public partial record PaymentMethodRequired;

    [ErrorCode("payment.operation.authentication_required")]
    public partial record AuthenticationRequired;

    [ErrorCode("payment.operation.declined")]
    public partial record Declined;

    [ErrorCode("payment.operation.expired")]
    public partial record Expired;

    [ErrorCode("payment.operation.canceled")]
    public partial record Canceled;

    [ErrorCode("payment.operation.conflict")]
    public partial record OperationConflict;

    [ErrorCode("payment.operation.provider_unavailable")]
    public partial record ProviderUnavailable;

    [ErrorCode("payment.operation.unknown")]
    public partial record Unknown;
}
