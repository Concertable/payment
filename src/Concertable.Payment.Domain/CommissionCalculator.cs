namespace Concertable.Payment.Domain;

internal sealed class CommissionCalculator
{
    public CommissionCalculation Calculate(
        long payeeGrossMinor,
        Currency currency,
        CommissionTerms terms,
        Percentage vatRate)
    {
        if (payeeGrossMinor < 0)
            throw new DomainException("Payee gross cannot be negative.");
        if (currency != Currency.Gbp)
            throw new DomainException("Commission currency must be GBP.");
        if (terms.Rate.IsZero)
            throw new DomainException("Commission rate must be greater than zero.");

        var commissionGrossMinor = terms.Rate.ApplyTo(payeeGrossMinor);
        var commissionNetMinor = vatRate.IsZero
            ? commissionGrossMinor
            : vatRate.ExcludeFrom(commissionGrossMinor);
        var commissionVatMinor = checked(commissionGrossMinor - commissionNetMinor);

        return new CommissionCalculation(
            currency,
            payeeGrossMinor,
            commissionGrossMinor,
            commissionNetMinor,
            commissionVatMinor,
            vatRate,
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
