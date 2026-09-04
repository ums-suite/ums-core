using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.UnitTests.Common;

public class NotificationCategoryExtensionsTests
{
    [Theory]
    [InlineData(NotificationCategory.Otp, true)]
    [InlineData(NotificationCategory.SecurityAlert, true)]
    [InlineData(NotificationCategory.Payment, true)]
    [InlineData(NotificationCategory.Result, true)]
    [InlineData(NotificationCategory.Transactional, true)]
    [InlineData(NotificationCategory.Informational, false)]
    public void IsMandatory_is_true_for_every_category_except_Informational(NotificationCategory category, bool expected) =>
        Assert.Equal(expected, category.IsMandatory());

    [Theory]
    [InlineData(NotificationCategory.Otp, true)]
    [InlineData(NotificationCategory.SecurityAlert, true)]
    [InlineData(NotificationCategory.Payment, false)]
    [InlineData(NotificationCategory.Result, false)]
    [InlineData(NotificationCategory.Transactional, false)]
    [InlineData(NotificationCategory.Informational, false)]
    public void IsExpeditedByDefault_is_true_only_for_Otp_and_SecurityAlert(NotificationCategory category, bool expected) =>
        Assert.Equal(expected, category.IsExpeditedByDefault());

    [Theory]
    [InlineData(NotificationCategory.Otp, true)]
    [InlineData(NotificationCategory.SecurityAlert, true)]
    [InlineData(NotificationCategory.Payment, true)]
    [InlineData(NotificationCategory.Result, true)]
    [InlineData(NotificationCategory.Transactional, false)]
    [InlineData(NotificationCategory.Informational, false)]
    public void IsAuditRequired_matches_requirement_spec_S5_auditability_list(NotificationCategory category, bool expected) =>
        Assert.Equal(expected, category.IsAuditRequired());
}
