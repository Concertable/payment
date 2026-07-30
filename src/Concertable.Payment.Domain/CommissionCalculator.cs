namespace Concertable.Payment.Domain;

internal sealed class CommissionCalculator
{
    public CommissionCalculation Calculate(
        long payeeGrossMinor,
        Currency currency,
        int rateBasisPoints,
        int vatRateBasisPoints)
    {
        if (payeeGrossMinor < 0)
            throw new DomainException("Payee gross cannot be negative.");
        if (currency != Currency.Gbp)
            throw new DomainException("Commission currency must be GBP.");
        if (rateBasisPoints is < 1 or > 10_000)
            throw new DomainException("Commission rate must be between 1 and 10,000 basis points.");
        if (vatRateBasisPoints is < 0 or > 10_000)
            throw new DomainException("Commission VAT rate must be between 0 and 10,000 basis points.");

        var commissionGrossMinor = DivideHalfUp(
            checked(payeeGrossMinor * rateBasisPoints),
            10_000);
        var commissionNetMinor = vatRateBasisPoints == 0
            ? commissionGrossMinor
            : DivideHalfUp(
                checked(commissionGrossMinor * 10_000),
                checked(10_000 + vatRateBasisPoints));
        var commissionVatMinor = checked(commissionGrossMinor - commissionNetMinor);

        return new CommissionCalculation(
            currency,
            payeeGrossMinor,
            commissionGrossMinor,
            commissionNetMinor,
            commissionVatMinor,
            vatRateBasisPoints,
            checked(payeeGrossMinor + commissionGrossMinor));
    }

    public long CalculateCumulativeRefund(
        long originalAmountMinor,
        long cumulativeGrossRefundMinor,
        long originalGrossMinor)
    {
        if (originalAmountMinor < 0)
            throw new DomainException("Original refund allocation cannot be negative.");
        if (originalGrossMinor <= 0)
            throw new DomainException("Original gross must be positive.");
        if (cumulativeGrossRefundMinor is < 0 || cumulativeGrossRefundMinor > originalGrossMinor)
            throw new DomainException("Cumulative gross refund exceeds the original gross.");

        return cumulativeGrossRefundMinor == originalGrossMinor
            ? originalAmountMinor
            : DivideHalfUp(
                checked(originalAmountMinor * cumulativeGrossRefundMinor),
                originalGrossMinor);
    }

    private static long DivideHalfUp(long numerator, long denominator) =>
        checked((numerator + (denominator / 2)) / denominator);
}
