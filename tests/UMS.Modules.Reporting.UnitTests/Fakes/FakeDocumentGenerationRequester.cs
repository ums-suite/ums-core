using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

public sealed class FakeDocumentGenerationRequester : IDocumentGenerationRequester
{
    public Result<GeneratedDocumentSummary> NextResult { get; set; } = new GeneratedDocumentSummary(Guid.NewGuid(), "Ready", "https://example.invalid/doc");

    public RequestDocumentGenerationCommand? LastCommand { get; private set; }

    public Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        return Task.FromResult(NextResult);
    }
}
