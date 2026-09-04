using System.Xml.Linq;
using Xunit;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentPublishedPackageReferenceTests
{
    private static readonly string[] AllowedConcertableDependencyPrefixes =
    [
        "Concertable.Contracts",
        "Concertable.DataAccess.",
        "Concertable.Grpc",
        "Concertable.Kernel",
        "Concertable.Messaging.",
        "Concertable.Payment.",
        "Concertable.Seed.Shared",
        "Concertable.ServiceDefaults",
        "Concertable.Shared.Api"
    ];

    [Fact]
    public void PaymentDeployableProjects_ReferenceOnlySharedOrPaymentAssemblies()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var unexpected = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".AppHost", StringComparison.Ordinal)
                && !Path.GetFileNameWithoutExtension(path).EndsWith(".Hosting", StringComparison.Ordinal))
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
                .Select(element => new
                {
                    Project = Path.GetRelativePath(sourceRoot, path),
                    Reference = (string?)element.Attribute("Include")
                }))
            .Where(item => item.Reference?.StartsWith("Concertable.", StringComparison.Ordinal) == true
                && !AllowedConcertableDependencyPrefixes.Any(prefix => item.Reference.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(unexpected);
    }
}
