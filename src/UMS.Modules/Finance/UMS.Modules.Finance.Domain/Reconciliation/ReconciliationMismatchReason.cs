namespace UMS.Modules.Finance.Domain.Reconciliation;

/// <summary>FIN-14: requirement-spec.md finance §3's module-local term - "a daily-reconciliation mismatch between Finance's internal PaymentTransaction state and the gateway settlement report."</summary>
public enum ReconciliationMismatchReason
{
    /// <summary>The gateway's settlement report for the target date has no record at all of this transaction.</summary>
    NoSettlementRecordFound,

    /// <summary>The gateway's settlement report reports a status other than Successful for a PaymentTransaction Finance itself recorded as Successful.</summary>
    GatewayStatusDisagreement,

    /// <summary>The gateway's settlement report reports a DIFFERENT gatewayTransactionId than the one Finance recorded for this attempt - defensive, should not occur with a real gateway's own stable id.</summary>
    GatewayTransactionIdMismatch,
}
