namespace UMS.Modules.Audit.Application.Retention;

/// <summary>One monthly partition's identity and range, as reported by <see cref="IPartitionInspector"/>.</summary>
public sealed record PartitionInfo(string Name, DateTimeOffset RangeStart, DateTimeOffset RangeEndExclusive);
