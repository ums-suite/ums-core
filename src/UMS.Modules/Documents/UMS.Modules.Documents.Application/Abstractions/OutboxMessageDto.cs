namespace UMS.Modules.Documents.Application.Abstractions;

public sealed record OutboxMessageDto(Guid Id, string EventType, string PayloadJson, int AttemptCount);
