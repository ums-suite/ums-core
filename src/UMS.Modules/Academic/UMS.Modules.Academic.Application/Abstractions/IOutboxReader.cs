namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>Diagnostic/testing read of Academic's own outbox table. Mirrors Faculty/Student's own copy exactly.</summary>
public interface IOutboxReader
{
    public Task<int> CountUnprocessedAsync(CancellationToken cancellationToken = default);
}
