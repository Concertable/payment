using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentOperationResolver
{
    Task<Result<string, PaymentOperationError>> ResolvePaymentMethodAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default);

    Task<Result<string, PaymentOperationError>> ResolveAuthorizationAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default);

    Task<string> ResolveProviderObjectIdAsync(
        PaymentOperationReference reference,
        CancellationToken ct = default);
}
