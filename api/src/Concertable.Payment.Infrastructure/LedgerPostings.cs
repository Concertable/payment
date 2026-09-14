namespace Concertable.Payment.Infrastructure;

internal static class LedgerPostings
{
    public static LedgerPosting DirectSettlement(SettlementTransactionEntity settlement) =>
        DirectSettlement(
            settlement.PayerId,
            settlement.PayeeId,
            Money.FromMinorUnits(settlement.PayeeGrossMinor, settlement.Currency),
            Money.FromMinorUnits(settlement.CommissionNetMinor, settlement.Currency),
            Money.FromMinorUnits(settlement.CommissionVatMinor, settlement.Currency),
            new PaymentOperationReference(settlement.OperationType, settlement.ClientReference),
            settlement.PaymentIntentId);

    public static LedgerPosting DirectSettlement(
        Guid payerId,
        Guid payeeId,
        Money gross,
        Money fee,
        PaymentOperationReference reference,
        string? paymentIntentId)
        => DirectSettlement(
            payerId,
            payeeId,
            gross,
            fee,
            Money.Zero(fee.Currency),
            reference,
            paymentIntentId);

    public static LedgerPosting DirectSettlement(
        Guid payerId,
        Guid payeeId,
        Money gross,
        Money commissionNet,
        Money commissionVat,
        PaymentOperationReference reference,
        string? paymentIntentId)
    {
        var legs = new List<PostingLeg>
        {
            new(
                new(LedgerAccountType.Receivable, payerId),
                LedgerDirection.Debit,
                gross + commissionNet + commissionVat),
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Credit, gross)
        };
        AddCommissionCreditLegs(legs, commissionNet, commissionVat);

        return new LedgerPosting(
            LedgerPostingType.DirectSettlement,
            RequireExternalId(paymentIntentId),
            reference,
            paymentIntentId,
            legs);
    }

    public static LedgerPosting EscrowHold(
        Guid payerId,
        Money total,
        PaymentOperationReference reference,
        string? paymentIntentId) =>
        new(LedgerPostingType.EscrowHold, RequireExternalId(paymentIntentId), reference, paymentIntentId,
        [
            new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Debit, total),
            new(new(LedgerAccountType.StripeClearing, null), LedgerDirection.Credit, total)
        ]);

    public static LedgerPosting EscrowRelease(
        Guid payeeId,
        Money gross,
        Money fee,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string transferId)
        => EscrowRelease(
            payeeId,
            gross,
            fee,
            Money.Zero(fee.Currency),
            reference,
            paymentIntentId,
            transferId);

    public static LedgerPosting EscrowRelease(
        Guid payeeId,
        Money gross,
        Money commissionNet,
        Money commissionVat,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string transferId)
    {
        var legs = new List<PostingLeg>
        {
            new(
                new(LedgerAccountType.StripeClearing, null),
                LedgerDirection.Debit,
                gross + commissionNet + commissionVat),
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Credit, gross)
        };
        AddCommissionCreditLegs(legs, commissionNet, commissionVat);

        return new LedgerPosting(
            LedgerPostingType.EscrowRelease,
            RequireExternalId(transferId),
            reference,
            paymentIntentId,
            legs);
    }

    public static LedgerPosting EscrowRefundBeforeRelease(
        Guid payerId,
        Money refunded,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string refundId) =>
        new(LedgerPostingType.EscrowRefund, RequireExternalId(refundId), reference, paymentIntentId,
        [
            new(new(LedgerAccountType.StripeClearing, null), LedgerDirection.Debit, refunded),
            new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Credit, refunded)
        ]);

    public static LedgerPosting EscrowRefundAfterRelease(
        Guid payerId,
        Guid payeeId,
        Money gross,
        Money fee,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string refundId)
        => EscrowRefundAfterRelease(
            payerId,
            payeeId,
            gross,
            fee,
            Money.Zero(fee.Currency),
            reference,
            paymentIntentId,
            refundId);

    public static LedgerPosting EscrowRefundAfterRelease(
        Guid payerId,
        Guid payeeId,
        Money gross,
        Money commissionNet,
        Money commissionVat,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string refundId)
    {
        var legs = new List<PostingLeg>
        {
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Debit, gross)
        };
        if (commissionNet.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.PlatformRevenue, null),
                LedgerDirection.Debit,
                commissionNet));
        if (commissionVat.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.VatLiability, null),
                LedgerDirection.Debit,
                commissionVat));
        legs.Add(new(
            new(LedgerAccountType.Receivable, payerId),
            LedgerDirection.Credit,
            gross + commissionNet + commissionVat));

        return new LedgerPosting(
            LedgerPostingType.EscrowRefund,
            RequireExternalId(refundId),
            reference,
            paymentIntentId,
            legs);
    }

    public static LedgerPosting DirectSettlementRefund(
        Guid payerId,
        Guid payeeId,
        Money gross,
        Money commissionNet,
        Money commissionVat,
        PaymentOperationReference reference,
        string? paymentIntentId,
        string refundId)
    {
        var legs = new List<PostingLeg>
        {
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Debit, gross)
        };
        if (commissionNet.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.PlatformRevenue, null),
                LedgerDirection.Debit,
                commissionNet));
        if (commissionVat.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.VatLiability, null),
                LedgerDirection.Debit,
                commissionVat));
        legs.Add(new(
            new(LedgerAccountType.Receivable, payerId),
            LedgerDirection.Credit,
            gross + commissionNet + commissionVat));

        return new LedgerPosting(
            LedgerPostingType.DirectSettlementRefund,
            RequireExternalId(refundId),
            reference,
            paymentIntentId,
            legs);
    }

    private static string RequireExternalId(string? externalId) =>
        !string.IsNullOrWhiteSpace(externalId)
            ? externalId
            : throw new DomainException("A ledger posting requires an external financial event id.");

    private static void AddCommissionCreditLegs(
        ICollection<PostingLeg> legs,
        Money commissionNet,
        Money commissionVat)
    {
        if (commissionNet.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.PlatformRevenue, null),
                LedgerDirection.Credit,
                commissionNet));
        if (commissionVat.ToMinorUnits() > 0)
            legs.Add(new(
                new(LedgerAccountType.VatLiability, null),
                LedgerDirection.Credit,
                commissionVat));
    }
}
