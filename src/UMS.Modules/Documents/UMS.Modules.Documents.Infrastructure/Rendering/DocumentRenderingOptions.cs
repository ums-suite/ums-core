namespace UMS.Modules.Documents.Infrastructure.Rendering;

/// <summary>Bound from <c>Documents:Rendering</c>.</summary>
public sealed class DocumentRenderingOptions
{
    /// <summary>The public base URL the in-PDF QR code encodes, e.g. <c>https://ums-suite.example/api/v1/documents/verify</c> - the renderer appends <c>/{digitalVerificationId}</c> (DOC-8).</summary>
    public string VerifyBaseUrl { get; set; } = "https://ums-suite.internal/api/v1/documents/verify";

    /// <summary>Printed in the document's header - a first-pass default until Organization (release/DEVELOPMENT_PLAN.md Flow #6) exists and can supply a real institution name/branding per requirement-spec.md documents §2's "university branding" note.</summary>
    public string InstitutionName { get; set; } = "University Management System";
}
