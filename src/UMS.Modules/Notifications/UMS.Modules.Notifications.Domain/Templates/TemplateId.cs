namespace UMS.Modules.Notifications.Domain.Templates;

public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
