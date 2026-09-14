extern alias PaymentInfrastructure;

using Concertable.Payment.Infrastructure.Grpc;
using ApiStatus = Concertable.Payment.Application.Enums.PayoutAccountStatus;
using ProtoStatus = PaymentInfrastructure::Concertable.Payment.Grpc.PayoutAccountStatusType;

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
    [InlineData(ApiStatus.NotVerified, ProtoStatus.PayoutNotVerified)]
    [InlineData(ApiStatus.Pending, ProtoStatus.PayoutPending)]
    [InlineData(ApiStatus.Verified, ProtoStatus.PayoutVerified)]
    public void ToProtoStatus_MapsEachApiValueToItsProtoCounterpart(ApiStatus api, ProtoStatus expected) =>
        Assert.Equal(expected, api.ToProtoStatus());
}
