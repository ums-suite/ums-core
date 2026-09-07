namespace UMS.Modules.Learning.Domain.Common;

/// <summary>ADR-0011's two supported languages, as this module's own <c>{table}_translations</c> key (ums-conventions.md, Localization Implementation). Module-local by the same reasoning Documents' own <c>LanguageCode</c> is module-local - no shared platform-wide enum exists.</summary>
public enum LanguageCode
{
    En,
    Bn,
}
