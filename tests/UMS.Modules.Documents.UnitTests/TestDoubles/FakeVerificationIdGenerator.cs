using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeVerificationIdGenerator : IVerificationIdGenerator
{
    public string NewId() => $"VERIFY-{Guid.NewGuid():N}";
}
