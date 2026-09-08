using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Employers;

/// <summary>
/// CAR-1: a staff-curated employer record (requirement-spec.md §2.1, §3 - module-local term pending
/// glossary merge). design-decisions.md "Employer Identity Model": no employer-facing `Identity`
/// login exists - `EmployerProfile` is created and maintained exclusively by Career-Services-staff/
/// Admin roles, referenced by both `Internship` and `CampusRecruitmentDrive` so a repeat recruiter's
/// details are entered once and reused. Not a top-level BRD aggregate, but modeled here as its own
/// <see cref="AggregateRoot{TId}"/> (uniform optimistic-concurrency treatment, matching every other
/// mutable entity in this module) since it has its own independent CRUD lifecycle even though it
/// never appears in a domain-event fan-out of its own (requirement-spec.md §3's event catalog names
/// no `EmployerProfile*` event).
/// </summary>
public sealed class EmployerProfile : AggregateRoot<EmployerProfileId>
{
    private EmployerProfile()
    {
    }

    private EmployerProfile(EmployerProfileId id, string companyName, string industry, string? website, string contactName, string contactEmail, string? contactPhone, string? verificationNote, DateTimeOffset createdAt)
    {
        Id = id;
        CompanyName = companyName;
        Industry = industry;
        Website = website;
        ContactName = contactName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        VerificationNote = verificationNote;
        CreatedAt = createdAt;
    }

    public string CompanyName { get; private set; } = string.Empty;

    public string Industry { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string ContactName { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public string? ContactPhone { get; private set; }

    public string? VerificationNote { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static EmployerProfile Create(string companyName, string industry, string? website, string contactName, string contactEmail, string? contactPhone, string? verificationNote, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("An EmployerProfile's company name is required.", nameof(companyName));
        }

        if (string.IsNullOrWhiteSpace(contactName))
        {
            throw new ArgumentException("An EmployerProfile's primary contact name is required.", nameof(contactName));
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new ArgumentException("An EmployerProfile's primary contact email is required.", nameof(contactEmail));
        }

        return new EmployerProfile(
            EmployerProfileId.New(),
            companyName.Trim(),
            industry?.Trim() ?? string.Empty,
            website?.Trim(),
            contactName.Trim(),
            contactEmail.Trim(),
            contactPhone?.Trim(),
            verificationNote?.Trim(),
            now);
    }

    /// <summary>Career-Services-staff-only edit (requirement-spec.md §2.1) - ownership/permission is enforced by the Api layer, never here.</summary>
    public void Update(string companyName, string industry, string? website, string contactName, string contactEmail, string? contactPhone, string? verificationNote)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("An EmployerProfile's company name is required.", nameof(companyName));
        }

        if (string.IsNullOrWhiteSpace(contactName))
        {
            throw new ArgumentException("An EmployerProfile's primary contact name is required.", nameof(contactName));
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new ArgumentException("An EmployerProfile's primary contact email is required.", nameof(contactEmail));
        }

        CompanyName = companyName.Trim();
        Industry = industry?.Trim() ?? string.Empty;
        Website = website?.Trim();
        ContactName = contactName.Trim();
        ContactEmail = contactEmail.Trim();
        ContactPhone = contactPhone?.Trim();
        VerificationNote = verificationNote?.Trim();
    }

    /// <summary>A relationship with a recruiter that has ended - kept for historical Internship/Drive references rather than hard-deleted (every foreign reference into this row must stay resolvable).</summary>
    public void Archive() => IsArchived = true;

    public void Unarchive() => IsArchived = false;
}
