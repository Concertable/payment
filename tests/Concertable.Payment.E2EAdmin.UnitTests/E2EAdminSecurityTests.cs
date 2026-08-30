using Concertable.Kernel;
using Concertable.Payment.E2ETests.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Concertable.Payment.E2EAdmin.UnitTests;

public sealed class E2EAdminSecurityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddPaymentE2EAdmin_BlankAdminKey_ThrowsInvalidOperationException(string? adminKey)
    {
        var configuration = Configuration(adminKey);
        var environment = new TestHostEnvironment(Environments.E2E);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPaymentE2EAdmin(configuration, environment));

        Assert.Contains("E2E:AdminKey", exception.Message);
    }

    [Fact]
    public void AddPaymentE2EAdmin_NonE2EEnvironment_ThrowsInvalidOperationException()
    {
        var configuration = Configuration("admin-key");
        var environment = new TestHostEnvironment(Environments.Development);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPaymentE2EAdmin(configuration, environment));

        Assert.Contains("E2E environment", exception.Message);
    }

    [Fact]
    public void IsAuthorized_MissingAdminKeyHeader_ReturnsFalse()
    {
        var headers = new HeaderDictionary();

        var authorized = E2EAdminSecurity.IsAuthorized("admin-key", headers);

        Assert.False(authorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IsAuthorized_BlankAdminKeyHeader_ReturnsFalse(string supplied)
    {
        var headers = new HeaderDictionary
        {
            ["X-Concertable-E2E-Key"] = supplied,
        };

        var authorized = E2EAdminSecurity.IsAuthorized("admin-key", headers);

        Assert.False(authorized);
    }

    private static IConfiguration Configuration(string? adminKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["E2E:AdminKey"] = adminKey,
                ["ConnectionStrings:PaymentDb"] = "Server=test",
            })
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            this.EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = nameof(E2EAdminSecurityTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
