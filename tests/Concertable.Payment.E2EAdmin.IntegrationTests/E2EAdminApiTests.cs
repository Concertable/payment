using System.Net;
using Concertable.Payment.E2ETests.Server;
using Concertable.Testing.Integration;
using Microsoft.Extensions.Hosting;

namespace Concertable.Payment.E2EAdmin.IntegrationTests;

public sealed class E2EAdminApiTests
{
    private const string AdminKey = "admin-key";
    private const string AdminKeyHeader = "X-Concertable-E2E-Key";
    private const string ConnectionStringName = "PaymentDb";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddPaymentE2EAdmin_BlankAdminKey_RejectsHostRegistration(string? adminKey)
    {
        var builder = E2EAdminTestHost.CreateBuilder(adminKey, ConnectionStringName);

        var exception = Should.Throw<InvalidOperationException>(
            () => builder.Services.AddPaymentE2EAdmin(builder.Configuration, builder.Environment));

        exception.Message.ShouldContain("E2E:AdminKey");
    }

    [Fact]
    public void AddPaymentE2EAdmin_NonE2EEnvironment_RejectsHostRegistration()
    {
        var builder = E2EAdminTestHost.CreateBuilder(AdminKey, ConnectionStringName, Environments.Development);

        var exception = Should.Throw<InvalidOperationException>(
            () => builder.Services.AddPaymentE2EAdmin(builder.Configuration, builder.Environment));

        exception.Message.ShouldContain("E2E environment");
    }

    [Fact]
    public async Task Reset_MissingAdminKeyHeader_ReturnsNotFound()
    {
        await using var host = await StartHostAsync();

        using var response = await host.Client.PostAsync("/_e2e/reset", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Reset_BlankAdminKeyHeader_ReturnsNotFound(string supplied)
    {
        await using var host = await StartHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/_e2e/reset");
        request.Headers.TryAddWithoutValidation(AdminKeyHeader, supplied).ShouldBeTrue();

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Task<E2EAdminTestHost> StartHostAsync() =>
        E2EAdminTestHost.StartAsync(
            AdminKey,
            ConnectionStringName,
            (services, configuration, environment) => services.AddPaymentE2EAdmin(configuration, environment),
            app => app.MapPaymentE2EAdmin());
}
