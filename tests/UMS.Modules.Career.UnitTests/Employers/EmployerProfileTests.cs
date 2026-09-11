using UMS.Modules.Career.Domain.Employers;

namespace UMS.Modules.Career.UnitTests.Employers;

/// <summary>CAR-1: design-decisions.md "Employer Identity Model" - no employer login exists, so this aggregate's only guards are the plain required-field invariants staff-entered data must satisfy.</summary>
public sealed class EmployerProfileTests
{
    private static EmployerProfile CreateProfile() =>
        EmployerProfile.Create("Acme Corp", "Software", "https://acme.example", "Jane Doe", "jane@acme.example", "+1-555-0100", null, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("", "Contact", "contact@acme.example")]
    [InlineData("Acme", "", "contact@acme.example")]
    [InlineData("Acme", "Contact", "")]
    public void Create_requires_company_name_contact_name_and_contact_email(string companyName, string contactName, string contactEmail)
    {
        Assert.Throws<ArgumentException>(() => EmployerProfile.Create(companyName, "Software", null, contactName, contactEmail, null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Archive_and_Unarchive_toggle_IsArchived()
    {
        var profile = CreateProfile();

        profile.Archive();
        Assert.True(profile.IsArchived);

        profile.Unarchive();
        Assert.False(profile.IsArchived);
    }

    [Fact]
    public void Update_overwrites_every_mutable_field()
    {
        var profile = CreateProfile();

        profile.Update("New Corp", "Finance", null, "John Smith", "john@new.example", null, "verified by phone");

        Assert.Equal("New Corp", profile.CompanyName);
        Assert.Equal("Finance", profile.Industry);
        Assert.Equal("John Smith", profile.ContactName);
        Assert.Equal("john@new.example", profile.ContactEmail);
        Assert.Equal("verified by phone", profile.VerificationNote);
    }
}
