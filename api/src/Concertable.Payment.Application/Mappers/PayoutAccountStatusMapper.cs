namespace Concertable.Payment.Application.Mappers;

internal static class PayoutAccountStatusMapper
{
    public static Enums.PayoutAccountStatus ToApiStatus(this PayoutAccountStatus status) => status switch
    {
        PayoutAccountStatus.NotVerified => Enums.PayoutAccountStatus.NotVerified,
        PayoutAccountStatus.Pending => Enums.PayoutAccountStatus.Pending,
        PayoutAccountStatus.Verified => Enums.PayoutAccountStatus.Verified,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
