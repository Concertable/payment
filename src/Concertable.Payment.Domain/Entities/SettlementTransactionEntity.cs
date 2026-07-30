namespace Concertable.Payment.Domain.Entities;

public sealed class SettlementTransactionEntity : TransactionEntity
{
    private SettlementTransactionEntity() { }

    private SettlementTransactionEntity(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        Currency currency,
        long payeeGrossMinor,
        long commissionGrossMinor,
        long commissionNetMinor,
        long commissionVatMinor,
        int commissionVatRateBasisPoints,
        TransactionStatus status,
        int bookingId,
        Guid? commissionAuthorizationId)
        : base(
            payerId,
            payeeId,
            paymentIntentId,
            checked(payeeGrossMinor + commissionGrossMinor),
            status)
    {
        BookingId = bookingId;
        Currency = currency;
        PayeeGrossMinor = payeeGrossMinor;
        CommissionGrossMinor = commissionGrossMinor;
        CommissionNetMinor = commissionNetMinor;
        CommissionVatMinor = commissionVatMinor;
        CommissionVatRateBasisPoints = commissionVatRateBasisPoints;
        PayerTotalMinor = checked(payeeGrossMinor + commissionGrossMinor);
        CommissionAuthorizationId = commissionAuthorizationId;
    }

    public override TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; private set; }
    public Guid? CommissionAuthorizationId { get; private set; }
    public CommissionAuthorizationEntity? CommissionAuthorization { get; private set; }
    public Currency Currency { get; private set; }
    public long PayeeGrossMinor { get; private set; }
    public long CommissionGrossMinor { get; private set; }
    public long CommissionNetMinor { get; private set; }
    public long CommissionVatMinor { get; private set; }
    public int CommissionVatRateBasisPoints { get; private set; }
    public long PayerTotalMinor { get; private set; }

    public static SettlementTransactionEntity Create(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        long platformFee,
        TransactionStatus status,
        int bookingId) =>
        new(
            payerId,
            payeeId,
            paymentIntentId,
            Currency.Gbp,
            checked(amount - platformFee),
            platformFee,
            platformFee,
            0,
            0,
            status,
            bookingId,
            null);

    internal static SettlementTransactionEntity CreateAuthorized(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        CommissionCalculation calculation,
        TransactionStatus status,
        int bookingId,
        Guid commissionAuthorizationId) =>
        new(
            payerId,
            payeeId,
            paymentIntentId,
            calculation.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.CommissionNetMinor,
            calculation.CommissionVatMinor,
            calculation.CommissionVatRateBasisPoints,
            status,
            bookingId,
            commissionAuthorizationId);
}
