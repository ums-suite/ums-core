namespace UMS.Modules.Research.Domain.Common;

/// <summary>Mirrors every other module's own copy exactly.</summary>
public interface IHasDomainEvents
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    public void ClearDomainEvents();
}
