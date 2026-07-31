using System.Reflection;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionServiceTests
{
    private const int RateBasisPoints = 1000;
    private const int VatRateBasisPoints = 2000;
    private const string ConfigurationVersion = "2024.1";
    private const long GrossMinor = 5000;

    private readonly Guid configurationId = Guid.NewGuid();

    private readonly Mock<ICommissionConfigurationRepository> configurationRepository = new();
    private readonly Mock<ICommissionAuthorizationRepository> authorizationRepository = new();
    private readonly Mock<ICommissionAuthorizationClaimRepository> claimRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly CommissionCalculator calculator = new();
    private readonly FakeTimeProvider timeProvider = new();

    private readonly CommissionConfigurationEntity configuration;

    public CommissionServiceTests()
    {
        configuration = CommissionConfigurationEntity.Create(
            configurationId, ConfigurationVersion, Currency.Gbp, RateBasisPoints, timeProvider.GetUtcNow());

        configurationRepository
            .Setup(r => r.GetByIdAsync(configurationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task PreviewAsync_CurrencyMismatch_Fails()
    {
        var result = await BuildService().PreviewAsync(GrossMinor, (Currency)840);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "currency_mismatch");
    }

    [Fact]
    public async Task PreviewAsync_MatchingCurrency_ReturnsQuote()
    {
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, RateBasisPoints, VatRateBasisPoints);

        var result = await BuildService().PreviewAsync(GrossMinor, Currency.Gbp);

        Assert.True(result.IsSuccess);
        Assert.Equal(configurationId, result.Value.CommissionConfigurationId);
        Assert.Equal(expected.PayeeGrossMinor, result.Value.GrossMinor);
        Assert.Equal(expected.CommissionGrossMinor, result.Value.CommissionMinor);
        Assert.Equal(expected.PayerTotalMinor, result.Value.PayerTotalMinor);
    }

    [Fact]
    public async Task CreateOrBindAuthorizationAsync_ReviewedConfigDiffers_ReturnsPricingChanged()
    {
        var result = await BuildService().CreateOrBindAuthorizationAsync(
            "booking:7", "payer:1", Currency.Gbp, Guid.NewGuid(), null, null, null, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "pricing_changed");
    }

    [Fact]
    public async Task CreateOrBindAuthorizationAsync_ExistingMatches_RebindsWithoutInserting()
    {
        var existing = CommissionAuthorizationEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAuthorizationAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, GrossMinor, 500, 5500);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.AuthorizationId);
        authorizationRepository.Verify(
            r => r.AddAsync(It.IsAny<CommissionAuthorizationEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrBindAuthorizationAsync_ExistingIntentDiffers_ReturnsMismatch()
    {
        var existing = CommissionAuthorizationEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAuthorizationAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_2", null, null, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_authorization_mismatch");
    }

    [Fact]
    public async Task CreateOrBindAuthorizationAsync_NoExisting_InsertsAndReturns()
    {
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionAuthorizationEntity?)null);

        var result = await BuildService().CreateOrBindAuthorizationAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, null, null, null);

        Assert.True(result.IsSuccess);
        authorizationRepository.Verify(
            r => r.AddAsync(It.IsAny<CommissionAuthorizationEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrBindAuthorizationAsync_ConcurrentInsertRace_RecoversExisting()
    {
        var existing = CommissionAuthorizationEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .SetupSequence(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionAuthorizationEntity?)null)
            .ReturnsAsync(existing);
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate identity"));

        var result = await BuildService().CreateOrBindAuthorizationAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.AuthorizationId);
    }

    [Fact]
    public async Task CalculateAuthorizedAsync_AuthorizationNotFound_Fails()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionAuthorizationEntity?)null);

        var result = await BuildService().CalculateAuthorizedAsync(
            id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_authorization_not_found");
    }

    [Fact]
    public async Task CalculateAuthorizedAsync_IdentityMismatch_Fails()
    {
        var authorization = AuthorizationWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(authorization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorization);

        var result = await BuildService().CalculateAuthorizedAsync(
            authorization.Id, "booking:OTHER", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, "pi_1", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_authorization_mismatch");
    }

    [Fact]
    public async Task CalculateAuthorizedAsync_BoundIntentDiffersFromSupplied_Fails()
    {
        var authorization = AuthorizationWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(authorization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorization);

        var result = await BuildService().CalculateAuthorizedAsync(
            authorization.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, "pi_2", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_authorization_intent_mismatch");
    }

    [Fact]
    public async Task CalculateAuthorizedAsync_ExactIdentityAndIntentMatch_ReturnsAuthorizedCommission()
    {
        var authorization = AuthorizationWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(authorization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorization);
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, RateBasisPoints, VatRateBasisPoints);

        var result = await BuildService().CalculateAuthorizedAsync(
            authorization.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor,
            expected.CommissionGrossMinor, expected.PayerTotalMinor, "pi_1", null);

        Assert.True(result.IsSuccess);
        Assert.Same(authorization, result.Value.Authorization);
        Assert.Same(configuration, result.Value.Configuration);
        Assert.Equal(expected, result.Value.Calculation);
    }

    [Fact]
    public async Task CalculateAuthorizedAsync_CalculationDiffersFromExpected_ReturnsPricingChanged()
    {
        var authorization = AuthorizationWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(authorization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorization);

        var result = await BuildService().CalculateAuthorizedAsync(
            authorization.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 1, 2, "pi_1", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "pricing_changed");
    }

    [Fact]
    public async Task ClaimAuthorizationAsync_FirstClaim_Succeeds()
    {
        var authorizationId = Guid.NewGuid();

        var result = await BuildService().ClaimAuthorizationAsync(
            authorizationId, CommissionAuthorizationConsumer.Escrow);

        Assert.True(result.IsSuccess);
        claimRepository.Verify(
            r => r.AddAsync(It.IsAny<CommissionAuthorizationClaimEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClaimAuthorizationAsync_DuplicateSameConsumer_IsIdempotent()
    {
        var authorizationId = Guid.NewGuid();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(DuplicateKeyDbUpdateException());
        claimRepository
            .Setup(r => r.GetByCommissionAuthorizationIdAsync(authorizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommissionAuthorizationClaimEntity.Create(
                authorizationId, CommissionAuthorizationConsumer.Escrow, timeProvider.GetUtcNow()));

        var result = await BuildService().ClaimAuthorizationAsync(
            authorizationId, CommissionAuthorizationConsumer.Escrow);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ClaimAuthorizationAsync_DuplicateDifferentConsumer_FailsClosed()
    {
        var authorizationId = Guid.NewGuid();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(DuplicateKeyDbUpdateException());
        claimRepository
            .Setup(r => r.GetByCommissionAuthorizationIdAsync(authorizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommissionAuthorizationClaimEntity.Create(
                authorizationId, CommissionAuthorizationConsumer.Escrow, timeProvider.GetUtcNow()));

        var result = await BuildService().ClaimAuthorizationAsync(
            authorizationId, CommissionAuthorizationConsumer.Settlement);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_authorization_already_consumed");
    }

    [Fact]
    public async Task ClaimAuthorizationAsync_DuplicateKeyButNoExistingClaim_Rethrows()
    {
        var authorizationId = Guid.NewGuid();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(DuplicateKeyDbUpdateException());
        claimRepository
            .Setup(r => r.GetByCommissionAuthorizationIdAsync(authorizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionAuthorizationClaimEntity?)null);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            BuildService().ClaimAuthorizationAsync(authorizationId, CommissionAuthorizationConsumer.Escrow));
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_ReturnsBoundIntent()
    {
        var authorization = CommissionAuthorizationEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(authorization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorization);

        var result = await BuildService().FindBoundPaymentIntentAsync(authorization.Id);

        Assert.Equal("pi_1", result);
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_NoAuthorization_ReturnsNull()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionAuthorizationEntity?)null);

        var result = await BuildService().FindBoundPaymentIntentAsync(id);

        Assert.Null(result);
    }

    private CommissionAuthorizationEntity AuthorizationWithConfiguration(
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId)
    {
        var authorization = CommissionAuthorizationEntity.Create(
            configurationId, externalReference, payerReference, timeProvider.GetUtcNow(), stripePaymentIntentId);
        typeof(CommissionAuthorizationEntity)
            .GetProperty(nameof(CommissionAuthorizationEntity.CommissionConfiguration))!
            .SetValue(authorization, configuration);
        return authorization;
    }

    private CommissionService BuildService() =>
        new(
            configurationRepository.Object,
            authorizationRepository.Object,
            claimRepository.Object,
            TestPaymentDbContext.Unopened(),
            unitOfWork.Object,
            calculator,
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = configurationId,
                Version = ConfigurationVersion,
                Currency = nameof(Currency.Gbp),
                RateBasisPoints = RateBasisPoints,
            }),
            Options.Create(new PlatformCommissionTaxOptions { VatRateBasisPoints = VatRateBasisPoints }),
            timeProvider);

    private static DbUpdateException DuplicateKeyDbUpdateException() =>
        new("unique key violation", SqlDuplicateKeyException());

    private static Exception SqlDuplicateKeyException()
    {
        var sqlExceptionType = Type.GetType(
            "Microsoft.Data.SqlClient.SqlException, Microsoft.Data.SqlClient", throwOnError: true)!;
        var assembly = sqlExceptionType.Assembly;
        var sqlErrorType = assembly.GetType("Microsoft.Data.SqlClient.SqlError", throwOnError: true)!;
        var sqlErrorCollectionType = assembly.GetType(
            "Microsoft.Data.SqlClient.SqlErrorCollection", throwOnError: true)!;

        var errorConstructor = sqlErrorType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First(c => c.GetParameters() is { Length: > 0 } p && p[0].ParameterType == typeof(int));
        var errorParameters = errorConstructor.GetParameters();
        var errorArguments = new object?[errorParameters.Length];
        errorArguments[0] = 2627;
        for (var i = 1; i < errorParameters.Length; i++)
            errorArguments[i] = DefaultArgument(errorParameters[i].ParameterType);
        var sqlError = errorConstructor.Invoke(errorArguments);

        var collection = Activator.CreateInstance(
            sqlErrorCollectionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;
        sqlErrorCollectionType
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(collection, [sqlError]);

        var createException = sqlExceptionType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .First(m => m.Name == "CreateException"
                && m.GetParameters() is { Length: 2 } p
                && p[0].ParameterType == sqlErrorCollectionType
                && p[1].ParameterType == typeof(string));
        return (Exception)createException.Invoke(null, [collection, "13.0.0"])!;
    }

    private static object? DefaultArgument(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
