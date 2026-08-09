using System.Xml.Linq;

namespace Concertable.Payment.UnitTests;

public sealed class ReunionArchitectureTests
{
    [Fact]
    public void PaymentSource_OldKernelFunctionalIdentities_AreAbsent()
    {
        var oldFunctionalNamespace = "Concertable.Kernel." + "Functional";
        var oldErrorsNamespace = "Concertable.Kernel." + "Errors";
        var violations = Directory
            .EnumerateFiles(FindPaymentRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains(oldFunctionalNamespace, StringComparison.Ordinal)
                    || source.Contains(oldErrorsNamespace, StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ReunionPackages_HaveOnlyDirectOwners()
    {
        var references = Directory
            .EnumerateFiles(FindPaymentRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path => XDocument
                .Load(path)
                .Descendants("PackageReference")
                .Select(reference => new
                {
                    Project = Path.GetFileNameWithoutExtension(path),
                    Package = (string?)reference.Attribute("Include")
                }))
            .Where(reference => reference.Package is "Reunion" or "Reunion.Errors" or "Reunion.AspNetCore")
            .Select(reference => $"{reference.Project}:{reference.Package}")
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "Concertable.Payment.Application:Reunion",
                "Concertable.Payment.Application:Reunion.Errors",
                "Concertable.Payment.Client:Reunion",
                "Concertable.Payment.Client:Reunion.Errors",
                "Concertable.Payment.Contracts:Reunion.Errors",
                "Concertable.Payment.Domain:Reunion",
                "Concertable.Payment.Domain:Reunion.Errors",
                "Concertable.Payment.Infrastructure:Reunion",
                "Concertable.Payment.Infrastructure:Reunion.Errors",
                "Concertable.Payment.IntegrationTests:Reunion",
                "Concertable.Payment.UnitTests:Reunion",
                "Concertable.Payment.UnitTests:Reunion.Errors"
            ],
            references);
    }

    private static bool IsGeneratedPath(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindPaymentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "Concertable.Payment.slnx");

            if (File.Exists(solution))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate api/Concertable.Payment.");
    }
}
