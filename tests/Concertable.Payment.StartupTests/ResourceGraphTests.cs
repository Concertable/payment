using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Concertable.Testing.Architecture;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Concertable.Payment.StartupTests;

public sealed class ResourceGraphTests
{
    [Fact]
    public async Task ProductionGraphAndStrictValidation_AreValid()
    {
        var validBuilder = AppHost.CreateBuilder([]);
        AssertImageEndpoint(validBuilder, AuthConstants.Resource, "https", scheme: "https");
        AssertContainerRuntimeArgs(validBuilder, AuthConstants.Resource, "--user", "root");
        AssertUsesDeveloperCertificate(validBuilder, AuthConstants.Resource);
        await AssertNoSpaClientsAsync(validBuilder);
        using var app = validBuilder.Build();
        var builder = AppHost.CreateBuilder([]);
        builder.Services.AddInvalidLifetimeGraph();
        Assert.ThrowsAny<Exception>(() => builder.Build());
    }

    [Fact]
    public async Task ProjectHostedPaymentWeb_AdvertisesNoDedicatedGrpcTransport()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sql = builder.AddSqlServer("sql");
        var paymentDb = sql.AddDatabase(PaymentConstants.Database);
        var asb = builder.AddAzureServiceBus("asb");
        var auth = builder.AddContainerImage(AuthConstants.Resource, "test-image", $"sha256:{new string('a', 64)}")
            .WithHttpsEndpoint(targetPort: 8080, name: "https");

        var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb).Resource;
        var environment = await GetRawEnvironmentAsync(paymentWeb, CancellationToken.None);

        // A project-hosted Payment has no cleartext gRPC port to publish, so it serves gRPC over the same
        // Aspire-allocated endpoint as REST. Naming a port here would turn that endpoint HTTP/2-only and
        // break every REST caller; only the container topology owns the split listener.
        Assert.DoesNotContain("PaymentTransport__GrpcPort", environment.Keys);
        Assert.DoesNotContain(
            paymentWeb.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "grpc");
    }

    private static async Task<Dictionary<string, object>> GetRawEnvironmentAsync(
        IResource resource, CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource, environment, cancellationToken);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray())
            await annotation.Callback(context);
        return environment;
    }

    private static async Task AssertNoSpaClientsAsync(IDistributedApplicationBuilder builder)
    {
        var auth = Assert.IsAssignableFrom<IResourceWithEnvironment>(
            builder.Resources.Single(resource => resource.Name == AuthConstants.Resource));
        var configuration = await ExecutionConfigurationBuilder.Create(auth)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
                NullLogger.Instance,
                CancellationToken.None);
        var environment = configuration.EnvironmentVariables.ToDictionary();

        Assert.Equal("true", environment["Auth__SpaClients__RestrictToEnabledClients"]);
        Assert.DoesNotContain(
            environment.Keys,
            key => key.StartsWith("Auth__SpaClients__EnabledClients__", StringComparison.Ordinal));
        Assert.DoesNotContain(
            environment.Keys,
            key => key.StartsWith("Auth__SpaClients__", StringComparison.Ordinal)
                && key != "Auth__SpaClients__RestrictToEnabledClients");
    }

    private static void AssertContainerRuntimeArgs(
        IDistributedApplicationBuilder builder,
        string resourceName,
        params object[] expected)
    {
        var resource = Assert.IsType<ServiceContainerResource>(
            builder.Resources.Single(resource => resource.Name == resourceName));
        var args = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
            annotation.Callback(new ContainerRuntimeArgsCallbackContext(args, CancellationToken.None))
                .GetAwaiter()
                .GetResult();

        Assert.Equal(expected, args);
    }

#pragma warning disable ASPIRECERTIFICATES001 // experimental API; asserts the temporary Auth image bridge
    private static void AssertUsesDeveloperCertificate(
        IDistributedApplicationBuilder builder,
        string resourceName)
    {
        var resource = Assert.IsType<ServiceContainerResource>(
            builder.Resources.Single(resource => resource.Name == resourceName));
        var certificate = Assert.Single(resource.Annotations.OfType<HttpsCertificateAnnotation>());

        Assert.True(certificate.UseDeveloperCertificate);
    }
#pragma warning restore ASPIRECERTIFICATES001

    private static void AssertImageEndpoint(
        IDistributedApplicationBuilder builder,
        string resourceName,
        string endpointName,
        string scheme)
    {
        var resource = Assert.IsType<ServiceContainerResource>(
            builder.Resources.Single(resource => resource.Name == resourceName));
        var endpoint = Assert.Single(
            resource.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == endpointName);

        Assert.Equal(endpointName, endpoint.Name);
        Assert.Equal(scheme, endpoint.UriScheme);
        Assert.Equal(8080, endpoint.TargetPort);
    }
}
