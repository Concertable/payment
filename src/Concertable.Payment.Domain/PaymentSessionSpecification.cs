namespace Concertable.Payment.Domain;

internal sealed class PaymentSessionSpecification
{
    private PaymentSessionSpecification(
        Guid operationId,
        PaymentSessionKind sessionKind,
        PaymentSession session,
        string operationType,
        string consumerCorrelation,
        string payerOwnerKey,
        string? payeeOwnerKey,
        long? amountMinor,
        Currency? currency,
        PaymentSessionFundsRouting fundsRouting,
        string? paymentMethodId,
        string providerCustomerId,
        string? providerConnectedAccountId)
    {
        OperationId = operationId;
        SessionKind = sessionKind;
        Session = session;
        OperationType = operationType;
        ConsumerCorrelation = consumerCorrelation;
        PayerOwnerKey = payerOwnerKey;
        PayeeOwnerKey = payeeOwnerKey;
        AmountMinor = amountMinor;
        Currency = currency;
        FundsRouting = fundsRouting;
        PaymentMethodId = paymentMethodId;
        ProviderCustomerId = providerCustomerId;
        ProviderConnectedAccountId = providerConnectedAccountId;
    }

    public Guid OperationId { get; }
    public PaymentSessionKind SessionKind { get; }
    public PaymentSession Session { get; }
    public string OperationType { get; }
    public string ConsumerCorrelation { get; }
    public string PayerOwnerKey { get; }
    public string? PayeeOwnerKey { get; }
    public long? AmountMinor { get; }
    public Currency? Currency { get; }
    public PaymentSessionFundsRouting FundsRouting { get; }
    public string? PaymentMethodId { get; }
    public string ProviderCustomerId { get; }
    public string? ProviderConnectedAccountId { get; }
    public PaymentSessionCaptureMode CaptureMode => SessionKind switch
    {
        PaymentSessionKind.Payment => PaymentSessionCaptureMode.Automatic,
        PaymentSessionKind.Authorization => PaymentSessionCaptureMode.Manual,
        PaymentSessionKind.PaymentMethodSetup or PaymentSessionKind.PaymentMethodVerification =>
            PaymentSessionCaptureMode.None,
        _ => throw new DomainException("Payment session kind is invalid.")
    };
    public static PaymentSessionSpecification Create(
        Guid operationId,
        PaymentSessionKind sessionKind,
        PaymentSession session,
        string operationType,
        string consumerCorrelation,
        string payerOwnerKey,
        string? payeeOwnerKey,
        long? amountMinor,
        Currency? currency,
        PaymentSessionFundsRouting fundsRouting,
        string? paymentMethodId,
        string providerCustomerId,
        string? providerConnectedAccountId)
    {
        if (operationId == Guid.Empty)
            throw new DomainException("Payment session operation id is required.");
        if (operationId.Version != 7)
            throw new DomainException("Payment session operation id must be UUIDv7.");
        if (!Enum.IsDefined(sessionKind))
            throw new DomainException("Payment session kind is invalid.");
        if (!Enum.IsDefined(session))
            throw new DomainException("Payment session mode is invalid.");
        if (!Enum.IsDefined(fundsRouting))
            throw new DomainException("Payment session funds routing is invalid.");

        operationType = Normalize(operationType, "Payment session operation type", 100);
        consumerCorrelation = Normalize(consumerCorrelation, "Payment session consumer correlation", 200);
        payerOwnerKey = Normalize(payerOwnerKey, "Payment session payer owner", 200);
        providerCustomerId = Normalize(providerCustomerId, "Payment session provider customer", 100);

        if (paymentMethodId is not null)
            paymentMethodId = Normalize(paymentMethodId, "Payment session payment method", 100);
        if (payeeOwnerKey is not null)
            payeeOwnerKey = Normalize(payeeOwnerKey, "Payment session payee owner", 200);
        if (providerConnectedAccountId is not null)
            providerConnectedAccountId = Normalize(
                providerConnectedAccountId,
                "Payment session provider connected account",
                100);

        if (sessionKind is PaymentSessionKind.Payment or PaymentSessionKind.Authorization)
        {
            if (amountMinor is null or <= 0)
                throw new DomainException("A money-moving payment session requires a positive minor-unit amount.");
            if (currency is null || !Enum.IsDefined(currency.Value))
                throw new DomainException("A money-moving payment session requires a valid currency.");
            if (payeeOwnerKey is null)
                throw new DomainException("A money-moving payment session requires a payee owner.");
            if (fundsRouting == PaymentSessionFundsRouting.None)
                throw new DomainException("A money-moving payment session requires funds routing.");
            if (fundsRouting == PaymentSessionFundsRouting.Destination && providerConnectedAccountId is null)
                throw new DomainException("Destination routing requires a provider connected account.");
            if (fundsRouting == PaymentSessionFundsRouting.Platform && providerConnectedAccountId is not null)
                throw new DomainException("Platform routing cannot bind a provider connected account.");
            if (session == PaymentSession.OffSession && paymentMethodId is null)
                throw new DomainException("An off-session payment requires a payment method.");
        }
        else if (amountMinor is not null
            || currency is not null
            || payeeOwnerKey is not null
            || fundsRouting != PaymentSessionFundsRouting.None
            || paymentMethodId is not null
            || providerConnectedAccountId is not null)
        {
            throw new DomainException("A setup payment session cannot contain money-movement inputs.");
        }

        return new(
            operationId,
            sessionKind,
            session,
            operationType,
            consumerCorrelation,
            payerOwnerKey,
            payeeOwnerKey,
            amountMinor,
            currency,
            fundsRouting,
            paymentMethodId,
            providerCustomerId,
            providerConnectedAccountId);
    }

    private static string Normalize(string value, string name, int maxLength)
    {
        DomainException.ThrowIfNullOrWhiteSpace(value, name);

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maxLength)
            throw new DomainException($"{name} cannot exceed {maxLength} characters.");

        return normalizedValue;
    }
}
