namespace Concertable.Payment.Domain;

internal sealed class PaymentSessionSpecification
{
    private PaymentSessionSpecification(
        Guid operationId,
        PaymentSessionKind sessionKind,
        string operationType,
        string consumerCorrelation,
        string payerOwnerKey,
        string? payeeOwnerKey,
        long? amountMinor,
        Currency? currency,
        PaymentSessionFundsRouting fundsRouting,
        string providerCustomerId,
        string? providerConnectedAccountId)
    {
        OperationId = operationId;
        SessionKind = sessionKind;
        OperationType = operationType;
        ConsumerCorrelation = consumerCorrelation;
        PayerOwnerKey = payerOwnerKey;
        PayeeOwnerKey = payeeOwnerKey;
        AmountMinor = amountMinor;
        Currency = currency;
        FundsRouting = fundsRouting;
        ProviderCustomerId = providerCustomerId;
        ProviderConnectedAccountId = providerConnectedAccountId;
    }

    public Guid OperationId { get; }
    public PaymentSessionKind SessionKind { get; }
    public string OperationType { get; }
    public string ConsumerCorrelation { get; }
    public string PayerOwnerKey { get; }
    public string? PayeeOwnerKey { get; }
    public long? AmountMinor { get; }
    public Currency? Currency { get; }
    public PaymentSessionFundsRouting FundsRouting { get; }
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
    public PaymentSessionCustomerPresence CustomerPresence => PaymentSessionCustomerPresence.OnSession;

    public static PaymentSessionSpecification Create(
        Guid operationId,
        PaymentSessionKind sessionKind,
        string operationType,
        string consumerCorrelation,
        string payerOwnerKey,
        string? payeeOwnerKey,
        long? amountMinor,
        Currency? currency,
        PaymentSessionFundsRouting fundsRouting,
        string providerCustomerId,
        string? providerConnectedAccountId)
    {
        if (operationId == Guid.Empty)
            throw new DomainException("Payment session operation id is required.");
        if (operationId.Version != 7)
            throw new DomainException("Payment session operation id must be UUIDv7.");
        if (!Enum.IsDefined(sessionKind))
            throw new DomainException("Payment session kind is invalid.");
        if (!Enum.IsDefined(fundsRouting))
            throw new DomainException("Payment session funds routing is invalid.");

        operationType = Required(operationType, "Payment session operation type", 100);
        consumerCorrelation = Required(consumerCorrelation, "Payment session consumer correlation", 200);
        payerOwnerKey = Required(payerOwnerKey, "Payment session payer owner", 200);
        providerCustomerId = Required(providerCustomerId, "Payment session provider customer", 100);
        payeeOwnerKey = Optional(payeeOwnerKey, "Payment session payee owner", 200);
        providerConnectedAccountId = Optional(
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
        }
        else if (amountMinor is not null
            || currency is not null
            || payeeOwnerKey is not null
            || fundsRouting != PaymentSessionFundsRouting.None
            || providerConnectedAccountId is not null)
        {
            throw new DomainException("A setup payment session cannot contain money-movement inputs.");
        }

        return new(
            operationId,
            sessionKind,
            operationType,
            consumerCorrelation,
            payerOwnerKey,
            payeeOwnerKey,
            amountMinor,
            currency,
            fundsRouting,
            providerCustomerId,
            providerConnectedAccountId);
    }

    private static string Required(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} is required.");

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new DomainException($"{name} cannot exceed {maxLength} characters.");

        return normalized;
    }

    private static string? Optional(string? value, string name, int maxLength) =>
        value is null ? null : Required(value, name, maxLength);
}
