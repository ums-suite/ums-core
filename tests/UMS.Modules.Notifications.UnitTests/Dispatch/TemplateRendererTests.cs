using UMS.Modules.Notifications.Application.Dispatch;

namespace UMS.Modules.Notifications.UnitTests.Dispatch;

public class TemplateRendererTests
{
    [Fact]
    public void Render_substitutes_known_merge_fields()
    {
        var result = TemplateRenderer.Render("Hello {{name}}, your balance is {{amount}}.", """{"name":"Rafi","amount":"1200"}""");

        Assert.Equal("Hello Rafi, your balance is 1200.", result);
    }

    [Fact]
    public void Render_leaves_unknown_placeholders_untouched()
    {
        var result = TemplateRenderer.Render("Hello {{name}}!", "{}");

        Assert.Equal("Hello {{name}}!", result);
    }

    [Fact]
    public void Render_tolerates_invalid_payload_json_by_treating_it_as_no_merge_fields()
    {
        var result = TemplateRenderer.Render("Hello {{name}}!", "not valid json");

        Assert.Equal("Hello {{name}}!", result);
    }
}
