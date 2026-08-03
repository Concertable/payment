using Concertable.Payment.Grpc;
using Concertable.Payment.Infrastructure.Grpc;
using ApiStatus = Concertable.Payment.Application.Enums.PayoutAccountStatus;

namespace Concertable.Payment.UnitTests.Mappers;

public sealed class PayoutAccountStatusMapperTests
{
    [Fact]
    public void ToApiStatus_MapsEachDomainValueToItsApiCounterpart()
    {
        Assert.Equal(ApiStatus.NotVerified, PayoutAccountStatus.NotVerified.ToApiStatus());
        Assert.Equal(ApiStatus.Pending, PayoutAccountStatus.Pending.ToApiStatus());
        Assert.Equal(ApiStatus.Verified, PayoutAccountStatus.Verified.ToApiStatus());
    }

    [Theory]
    [InlineData(ApiStatus.NotVerified, PayoutAccountStatusType.PayoutNotVerified)]
    [InlineData(ApiStatus.Pending, PayoutAccountStatusType.PayoutPending)]
    [InlineData(ApiStatus.Verified, PayoutAccountStatusType.PayoutVerified)]
    public void ToProtoStatus_MapsEachApiValueToItsProtoCounterpart(ApiStatus api, PayoutAccountStatusType expected) =>
        Assert.Equal(expected, api.ToProtoStatus());
}
