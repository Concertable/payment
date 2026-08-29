using Concertable.Kernel;
using static Concertable.Payment.Domain.Lifecycle.PaymentOperationTrigger;

namespace Concertable.Payment.Domain.Lifecycle;

internal sealed class PaymentRefundStateMachine : StateMachine<PaymentOperationState, PaymentOperationTrigger>
{
    public PaymentRefundStateMachine()
        : base(
        [
            (PaymentOperationState.Processing, Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresAction, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Succeeded, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Canceled, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Failed, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Creating, Process, PaymentOperationState.Processing),
            (PaymentOperationState.Creating, RequireAction, PaymentOperationState.RequiresAction),

            (PaymentOperationState.Processing, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Processing, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Processing, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Processing, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresAction, Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresAction, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresAction, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresAction, Fail, PaymentOperationState.Failed)
        ])
    {
    }
}
