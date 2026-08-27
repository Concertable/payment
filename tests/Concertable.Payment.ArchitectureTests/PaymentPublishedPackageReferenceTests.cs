extern alias PaymentClient;

using System.Xml.Linq;
using Concertable.Payment.Contracts.Events;
using Concertable.Testing;
using Xunit;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentPublishedPackageReferenceTests
{
    [Fact]
    public void PublishedAssemblies_DoNotReferenceProviderOrConsumerRuntime()
    {
        var assemblies = new[]
        {
            typeof(PaymentOperationStateChanged).Assembly,
            typeof(ClientSnapshot).Assembly
        };

        var forbidden = assemblies
            .SelectMany(assembly => assembly.ReferencesToAssembliesStartingWith("Stripe", "Concertable.B2B", "Concertable.Customer"));

        Assert.Empty(forbidden);
    }

    [Fact]
    public void PaymentDeployableProjects_DoNotReferenceConsumerAssemblies()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var forbidden = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".AppHost", StringComparison.Ordinal)
                && !Path.GetFileNameWithoutExtension(path).EndsWith(".Hosting", StringComparison.Ordinal))
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
                .Select(element => new
                {
                    Project = Path.GetRelativePath(sourceRoot, path),
                    Reference = (string?)element.Attribute("Include")
                }))
            .Where(item => item.Reference is not null && (item.Reference.Contains("Concertable.B2B", StringComparison.Ordinal)
                || item.Reference.Contains("Concertable.Customer", StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }
}
