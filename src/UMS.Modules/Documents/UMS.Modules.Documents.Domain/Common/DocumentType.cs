namespace UMS.Modules.Documents.Domain.Common;

/// <summary>
/// The document types in scope for v1 (requirement-spec.md documents §1's table), each with its
/// own triggering module and sync/async default enforced at the Application layer, not here -
/// this enum only names the type, it does not encode which path a given request must take.
/// </summary>
public enum DocumentType
{
    AdmitCard = 0,
    MeritList = 1,
    Transcript = 2,
    Certificate = 3,
    IdCard = 4,
    Receipt = 5,
}
