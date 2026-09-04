using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeOutboxEnqueuer : IOutboxEnqueuer
{
    public List<(string EventType, string PayloadJson)> Enqueued { get; } = [];

    public void Enqueue(string eventType, string payloadJson, DateTimeOffset occurredAt) => Enqueued.Add((eventType, payloadJson));
}
