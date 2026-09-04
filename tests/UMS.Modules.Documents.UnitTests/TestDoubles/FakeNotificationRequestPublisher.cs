using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeNotificationRequestPublisher : INotificationRequestPublisher
{
    public List<NotificationRequest> Published { get; } = [];

    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        Published.Add(request);
        return Task.CompletedTask;
    }
}
