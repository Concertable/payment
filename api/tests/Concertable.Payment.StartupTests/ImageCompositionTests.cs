using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Xunit;

namespace Concertable.Payment.StartupTests;

/// <summary>Covers the image overload of Payment's own web service, which no AppHost in this repository
/// composes. Payment declared two endpoints against one container port — identical but for their names —
/// and Docker refuses the second binding, so a consumer composing Payment by image got a container that
/// never started, reporting only that the address was already in use.</summary>
public sealed class ImageCompositionTests
{
    private const string Digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public void AddPaymentWeb_ByImage_DeclaresOneEndpointPerContainerPort()
    {
        var web = ComposePaymentWebByImage();

        var targetPorts = web.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.TargetPort)
            .ToList();

        Assert.Equal(targetPorts.Distinct(), targetPorts);
        Assert.Equal(
            [PaymentConstants.HttpPort, PaymentConstants.GrpcPort],
            targetPorts.OrderBy(port => port));
    }

    [Fact]
    public void AddPaymentWeb_ByImage_ServesItsPrimaryEndpointAsPlaintext()
    {
        var web = ComposePaymentWebByImage();

        var primary = Assert.Single(
            web.Resource.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "https");

        Assert.Equal("http", primary.UriScheme);
        Assert.Equal(PaymentConstants.HttpPort, primary.TargetPort);
    }

    private static IResourceBuilder<ServiceContainerResource> ComposePaymentWebByImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sql = builder.AddSqlServer("sql");
        var auth = builder.AddContainerImage(AuthConstants.Resource, "ghcr.io/concertable/auth", Digest)
                          .WithHttpEndpoint(targetPort: AuthConstants.ContainerPort, name: "https");

        return builder.AddPaymentWeb(
            "ghcr.io/concertable/payment-web",
            Digest,
            auth,
            sql.AddDatabase(PaymentConstants.Database),
            builder.AddAzureServiceBus("asb"));
    }
}
