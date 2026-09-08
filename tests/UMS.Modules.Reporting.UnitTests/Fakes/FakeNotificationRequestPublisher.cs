using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

public sealed class FakeNotificationRequestPublisher : INotificationRequestPublisher
{
    public List<ReportingNotificationRequest> Published { get; } = [];

    public Exception? ThrowOnPublish { get; set; }

    public Task PublishAsync(ReportingNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish is not null)
        {
            throw ThrowOnPublish;
        }

        Published.Add(request);
        return Task.CompletedTask;
    }
}
