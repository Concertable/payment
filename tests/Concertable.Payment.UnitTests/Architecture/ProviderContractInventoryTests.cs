using System.Text.Json;
using System.Text.RegularExpressions;

namespace Concertable.Payment.UnitTests.Architecture;

public sealed partial class ProviderContractInventoryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly ProviderContractInventory Inventory = LoadInventory();
    private static readonly HashSet<string> DiscoveredKeys = DiscoverEntryPoints()
        .Select(entry => entry.Key)
        .ToHashSet(StringComparer.Ordinal);

    public static IEnumerable<object[]> CommittedEntryPoints =>
        Inventory.EntryPoints.Select(entry => new object[] { entry });

    [Fact]
    public void SourceEntryPoints_CurrentScanMatchesCommittedInventory()
    {
        var expected = Inventory.EntryPoints.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var unclassified = DiscoveredKeys.Except(expected).Order(StringComparer.Ordinal).ToArray();
        var missing = expected.Except(DiscoveredKeys).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            unclassified.Length == 0 && missing.Length == 0,
            $"Unclassified entry points:{Environment.NewLine}{string.Join(Environment.NewLine, unclassified)}{Environment.NewLine}{Environment.NewLine}Missing committed entry points:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Theory]
    [MemberData(nameof(CommittedEntryPoints))]
    public void CommittedEntryPoint_StillExistsAndHasACompleteDecision(ProviderContractEntryPoint entry)
    {
        var decision = Assert.Single(Inventory.Decisions, decision => decision.Id == entry.DecisionId);

        Assert.Contains(entry.Key, DiscoveredKeys);
        Assert.All(
            new[] { decision.Owner, decision.Flow, decision.ProviderProduct, decision.Mode, decision.ConnectModel, decision.Identity, decision.Compatibility },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void ScanRoots_ContainEveryProviderSurface()
    {
        string[] expected =
        [
            "api/Concertable.Payment/src|payment",
            "api/Concertable.Customer/src|consumer",
            "api/Concertable.B2B/src|consumer",
            "app/web/customer/src|frontend",
            "app/web/b2b|frontend",
            "app/web/shared/src|frontend",
            "app/mobile/customer/src|frontend"
        ];

        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            Inventory.ScanRoots.Select(root => $"{root.Path}|{root.Detector}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Decisions_AreUniqueAndReferenced()
    {
        Assert.Equal(1, Inventory.SchemaVersion);
        Assert.Equal(Inventory.Decisions.Count, Inventory.Decisions.Select(decision => decision.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Inventory.EntryPoints.Count, Inventory.EntryPoints.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(
            Inventory.Decisions
                .Select(decision => decision.Id)
                .Except(Inventory.EntryPoints.Select(entry => entry.DecisionId), StringComparer.Ordinal));
    }

    private static ProviderContractInventory LoadInventory()
    {
        var path = Path.Combine(RepositoryRoot, "api", "Concertable.Payment", "provider-contract-inventory.json");
        return JsonSerializer.Deserialize<ProviderContractInventory>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Provider contract inventory could not be deserialized.");
    }

    private static IReadOnlyList<DiscoveredEntryPoint> DiscoverEntryPoints()
    {
        var discovered = Inventory.ScanRoots
            .SelectMany(DiscoverEntryPoints)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Kind, StringComparer.Ordinal)
            .ThenBy(entry => entry.Member, StringComparer.Ordinal)
            .ThenBy(entry => entry.Operation, StringComparer.Ordinal)
            .ToArray();

        return discovered
            .GroupBy(entry => new { entry.Path, entry.Kind, entry.Member, entry.Operation })
            .SelectMany(group => group.Select((entry, index) => entry with { Occurrence = index + 1 }))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverEntryPoints(ProviderContractScanRoot root)
    {
        var absoluteRoot = Path.Combine(RepositoryRoot, root.Path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(absoluteRoot), $"Inventory scan root does not exist: {root.Path}");

        var extension = root.Detector == "frontend" ? "*.ts*" : "*.cs";
        foreach (var path in Directory.EnumerateFiles(absoluteRoot, extension, SearchOption.AllDirectories))
        {
            if (IsGeneratedOrTestPath(path))
                continue;

            var source = File.ReadAllText(path);
            var relativePath = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

            foreach (var entry in root.Detector switch
            {
                "payment" => DiscoverPaymentEntries(relativePath, source),
                "consumer" => DiscoverConsumerEntries(relativePath, source),
                "frontend" => DiscoverFrontendEntries(relativePath, source),
                _ => throw new InvalidOperationException($"Unknown inventory detector: {root.Detector}")
            })
                yield return entry;
        }
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverPaymentEntries(string path, string source)
    {
        var stripeServiceReceivers = StripeImportPattern().IsMatch(source)
            ? StripeServiceFieldPattern()
                .Matches(source)
                .Select(match => match.Groups["receiver"].Value)
                .ToHashSet(StringComparer.Ordinal)
            : [];
        foreach (Match match in AsyncCallPattern().Matches(source))
        {
            var receiver = match.Groups["receiver"].Value;
            if (stripeServiceReceivers.Contains(receiver))
                yield return Entry(path, "provider-api", source, match, $"{receiver}.{match.Groups["operation"].Value}");
        }

        foreach (Match match in WebhookIngressPattern().Matches(source))
            yield return Entry(path, "webhook-ingress", source, match, "EventUtility.ValidateSignature");
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverConsumerEntries(string path, string source)
    {
        foreach (Match match in ConsumerCallPattern().Matches(source))
            yield return Entry(path, "consumer-call", source, match, $"{match.Groups["receiver"].Value}.{match.Groups["operation"].Value}");
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverFrontendEntries(string path, string source)
    {
        foreach (Match match in FrontendConfirmationPattern().Matches(source))
            yield return new DiscoveredEntryPoint(path, "frontend-confirmation", null, match.Groups["operation"].Value, 0);

        foreach (Match match in ClientSecretParserPattern().Matches(source))
        {
            var operation = match.Groups["parser"].Value == "split"
                ? "client-secret-id-split"
                : "client-secret-kind-prefix";
            yield return new DiscoveredEntryPoint(path, "client-secret-parser", null, operation, 0);
        }
    }

    private static DiscoveredEntryPoint Entry(string path, string kind, string source, Match match, string operation) =>
        new(path, kind, FindContainingMember(source, match.Index), operation, 0);

    private static string FindContainingMember(string source, int invocationIndex)
    {
        var declaration = MethodDeclarationPattern()
            .Matches(source)
            .Cast<Match>()
            .LastOrDefault(match => match.Index < invocationIndex);
        return declaration?.Groups["member"].Value
            ?? throw new InvalidOperationException("Could not identify the member containing an inventoried call.");
    }

    private static bool IsGeneratedOrTestPath(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}Tests{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}__tests__{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}node_modules{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}dist{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}coverage{separator}", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path).Contains(".test.", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path).Contains(".spec.", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "api", "Concertable.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [GeneratedRegex(@"\busing\s+Stripe\s*;")]
    private static partial Regex StripeImportPattern();

    [GeneratedRegex(@"\bprivate\s+readonly\s+(?:Stripe\.)?(?:(?!I[A-Z])[A-Z][A-Za-z0-9]*Service|StripeClient)\s+(?<receiver>[A-Za-z_]\w*)\s*;")]
    private static partial Regex StripeServiceFieldPattern();

    [GeneratedRegex(@"\b(?<receiver>[A-Za-z_]\w*)\.(?<operation>[A-Za-z_]\w*Async)\s*\(")]
    private static partial Regex AsyncCallPattern();

    [GeneratedRegex(@"\b(?<receiver>customerPaymentClient|managerPaymentClient|escrowClient|payoutAccountClient)\.(?<operation>[A-Za-z_]\w*Async)\s*\(")]
    private static partial Regex ConsumerCallPattern();

    [GeneratedRegex(@"\bEventUtility\.ValidateSignature\s*\(")]
    private static partial Regex WebhookIngressPattern();

    [GeneratedRegex(@"\b(?:stripe\.)?(?<operation>confirm[A-Z][A-Za-z0-9]*|handleNextAction|initPaymentSheet|presentPaymentSheet)\s*\(")]
    private static partial Regex FrontendConfirmationPattern();

    [GeneratedRegex(@"\b(?:clientSecret|client_secret)\b[^;\r\n]{0,120}\.(?<parser>split|startsWith|substring|slice|match)\s*\(")]
    private static partial Regex ClientSecretParserPattern();

    [GeneratedRegex(@"(?m)^\s*(?:public|private|internal|protected)\s+(?:async\s+)?[^\r\n{;=]+?\s+(?<member>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex MethodDeclarationPattern();
}

public sealed record ProviderContractInventory(
    int SchemaVersion,
    IReadOnlyList<ProviderContractScanRoot> ScanRoots,
    IReadOnlyList<ProviderContractDecision> Decisions,
    IReadOnlyList<ProviderContractEntryPoint> EntryPoints);

public sealed record ProviderContractScanRoot(string Path, string Detector);

public sealed record ProviderContractDecision(
    string Id,
    string Owner,
    string Flow,
    string ProviderProduct,
    string Mode,
    string ConnectModel,
    string Identity,
    string Compatibility);

public sealed record ProviderContractEntryPoint(
    string Path,
    string Kind,
    string? Member,
    string Operation,
    int Occurrence,
    string DecisionId)
{
    public string Key => $"{Path}|{Kind}|{Member ?? "-"}|{Operation}|{Occurrence}";
}

internal sealed record DiscoveredEntryPoint(
    string Path,
    string Kind,
    string? Member,
    string Operation,
    int Occurrence)
{
    public string Key => $"{Path}|{Kind}|{Member ?? "-"}|{Operation}|{Occurrence}";
}
