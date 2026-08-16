using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Concertable.Payment.UnitTests.Architecture;

public sealed partial class ProviderContractInventoryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly ProviderContractInventory Inventory = LoadInventory();
    private static readonly IReadOnlyList<MetadataReference> CompilationReferences = CreateCompilationReferences();
    private static readonly HashSet<string> DiscoveredKeys = DiscoverEntryPoints()
        .Select(entry => entry.Key)
        .ToHashSet(StringComparer.Ordinal);

    public static IEnumerable<object[]> CommittedEntryPoints =>
        Inventory.EntryPoints.Select(entry => new object[] { entry });

    public static TheoryData<string, string?, string> StripeReceiverSyntaxCases => new()
    {
        {
            """
            namespace Example;

            public sealed class Adapter
            {
                private readonly Stripe.PaymentIntentService paymentIntentService;

                public Adapter(Stripe.PaymentIntentService paymentIntentService)
                {
                    this.paymentIntentService = paymentIntentService;
                }

                public void Execute()
                {
                    _ = this.paymentIntentService.CreateAsync(null!);
                }
            }
            """,
            null,
            "this.paymentIntentService.CreateAsync"
        },
        {
            """
            using Stripe.Checkout;

            namespace Example;

            public sealed class Adapter(SessionService sessionService)
            {
                public void Execute()
                {
                    _ = sessionService.CreateAsync(null!);
                }
            }
            """,
            null,
            "sessionService.CreateAsync"
        },
        {
            """
            namespace Example;

            public sealed class Adapter(PaymentIntentService paymentIntentService)
            {
                public void Execute()
                {
                    _ = paymentIntentService.GetAsync("pi_test");
                }
            }
            """,
            "global using Stripe;",
            "paymentIntentService.GetAsync"
        },
        {
            """
            using Stripe;

            namespace Example;

            public sealed class Adapter(StripeClient stripeClient)
            {
                public void Execute()
                {
                    _ = stripeClient.RequestAsync<object>(null!, null!, null!, null);
                }
            }
            """,
            null,
            "stripeClient.RequestAsync"
        }
    };

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

    [Theory]
    [MemberData(nameof(StripeReceiverSyntaxCases))]
    public void DiscoverPaymentEntries_StripeReceiverSyntax_DiscoversEntryPoint(
        string source,
        string? globalUsingsSource,
        string expectedOperation)
    {
        var sources = new List<PaymentSourceFile>
        {
            new("Example.cs", source, "Example")
        };
        if (globalUsingsSource is not null)
            sources.Add(new PaymentSourceFile("GlobalUsings.cs", globalUsingsSource, "Example"));

        var entry = Assert.Single(DiscoverPaymentEntries(sources));

        Assert.Equal(expectedOperation, entry.Operation);
        Assert.Equal("Execute", entry.Member);
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
        var paths = Directory
            .EnumerateFiles(absoluteRoot, extension, SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestPath(path))
            .ToArray();

        if (root.Detector == "payment")
        {
            var sources = paths
                .Select(path => new PaymentSourceFile(
                    Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/'),
                    File.ReadAllText(path),
                    FindContainingProject(path, absoluteRoot)))
                .ToArray();
            foreach (var entry in DiscoverPaymentEntries(sources))
                yield return entry;
            yield break;
        }

        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            var relativePath = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

            foreach (var entry in root.Detector switch
            {
                "consumer" => DiscoverConsumerEntries(relativePath, source),
                "frontend" => DiscoverFrontendEntries(relativePath, source),
                _ => throw new InvalidOperationException($"Unknown inventory detector: {root.Detector}")
            })
                yield return entry;
        }
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverPaymentEntries(IReadOnlyCollection<PaymentSourceFile> sources)
    {
        foreach (var projectSources in sources.GroupBy(source => source.Project, StringComparer.OrdinalIgnoreCase))
        {
            var syntaxTrees = projectSources
                .Select(source => CSharpSyntaxTree.ParseText(source.Source, path: source.Path))
                .ToArray();
            var compilation = CSharpCompilation.Create(
                $"ProviderContractInventory_{Guid.NewGuid():N}",
                syntaxTrees,
                CompilationReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            foreach (var syntaxTree in syntaxTrees)
            {
                var source = projectSources.Single(source => source.Path == syntaxTree.FilePath);
                var root = syntaxTree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                        || !memberAccess.Name.Identifier.ValueText.EndsWith("Async", StringComparison.Ordinal)
                        || !IsStripeSdkType(semanticModel.GetTypeInfo(memberAccess.Expression).Type))
                        continue;

                    yield return new DiscoveredEntryPoint(
                        source.Path,
                        "provider-api",
                        FindContainingMember(invocation),
                        $"{memberAccess.Expression}.{memberAccess.Name.Identifier.ValueText}",
                        0);
                }

                foreach (Match match in WebhookIngressPattern().Matches(source.Source))
                    yield return Entry(source.Path, "webhook-ingress", source.Source, match, "EventUtility.ValidateSignature");
            }
        }
    }

    private static bool IsStripeSdkType(ITypeSymbol? type) =>
        type?.ContainingNamespace.ToDisplayString() is "Stripe"
            || type?.ContainingNamespace.ToDisplayString().StartsWith("Stripe.", StringComparison.Ordinal) == true;

    private static string FindContainingMember(InvocationExpressionSyntax invocation) =>
        invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText
        ?? throw new InvalidOperationException("Could not identify the member containing an inventoried call.");

    private static string FindContainingProject(string path, string scanRoot)
    {
        for (var directory = Directory.GetParent(path); directory is not null; directory = directory.Parent)
        {
            var project = Directory.EnumerateFiles(directory.FullName, "*.csproj", SearchOption.TopDirectoryOnly).SingleOrDefault();
            if (project is not null)
                return project;
            if (string.Equals(directory.FullName, scanRoot, StringComparison.OrdinalIgnoreCase))
                break;
        }

        throw new InvalidOperationException($"Could not identify the project containing {path}.");
    }

    private static IReadOnlyList<MetadataReference> CreateCompilationReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(Stripe.StripeClient).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static IEnumerable<DiscoveredEntryPoint> DiscoverConsumerEntries(string path, string source)
    {
        foreach (Match match in ConsumerCallPattern().Matches(source))
            yield return Entry(path, "consumer-call", source, match, $"{match.Groups["receiver"].Value}.{match.Groups["operation"].Value}");

        foreach (Match match in ConsumerCommandPattern().Matches(source))
            yield return Entry(path, "consumer-call", source, match, $"bus.SendAsync<{match.Groups["command"].Value}>");
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

    [GeneratedRegex(@"\b(?<receiver>customerPaymentClient|managerPaymentClient|escrowClient|payoutAccountClient)\.(?<operation>[A-Za-z_]\w*Async)\s*\(")]
    private static partial Regex ConsumerCallPattern();

    [GeneratedRegex(@"\bbus\.SendAsync\s*\(\s*new\s+(?<command>CaptureEscrowCommand|DepositEscrowCommand|RefundEscrowCommand)\s*\(")]
    private static partial Regex ConsumerCommandPattern();

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

internal sealed record PaymentSourceFile(string Path, string Source, string Project);
