namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>requirement-spec.md §2/§9: "Routed back through IPaymentGateway when the provider supports programmatic refund; otherwise recorded as a manually-settled refund an operator marks complete."</summary>
public enum RefundMethod
{
    GatewayRouted,
    ManuallySettled,
}
