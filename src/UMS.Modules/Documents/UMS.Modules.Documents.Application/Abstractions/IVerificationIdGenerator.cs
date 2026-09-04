namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>DOC-8: generates a unique, unguessable <c>digitalVerificationId</c> (requirement-spec.md documents §2/§4) - its own seam so the unguessability mechanism (cryptographic randomness) is swappable/testable independently of the aggregate.</summary>
public interface IVerificationIdGenerator
{
    public string NewId();
}
