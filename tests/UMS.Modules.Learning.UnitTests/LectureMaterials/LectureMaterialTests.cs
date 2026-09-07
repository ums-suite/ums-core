using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.Events;
using UMS.Modules.Learning.Domain.LectureMaterials;

namespace UMS.Modules.Learning.UnitTests.LectureMaterials;

/// <summary>LRN-12/LRN-14 / design-decisions.md "LectureMaterial Versioning &amp; Retrieval Default": append-only versions, default retrieval resolves to latest, every prior version stays individually addressable.</summary>
public sealed class LectureMaterialTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_material_without_a_module_group_is_rejected()
    {
        var result = LectureMaterial.Create(Guid.NewGuid(), LectureMaterialType.Document, "  ", 0, Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("lecture_material.module_group_required", result.Error!.Code);
    }

    [Fact]
    public void A_negative_sort_order_is_rejected()
    {
        var result = LectureMaterial.Create(Guid.NewGuid(), LectureMaterialType.Document, "Week 1", -1, Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("lecture_material.sort_order_negative", result.Error!.Code);
    }

    [Fact]
    public void The_first_version_raises_LectureMaterialPublished()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Lecture 1", null);

        var version = material.PublishNewVersion(Guid.NewGuid(), null, null, Guid.NewGuid(), Now);

        Assert.True(version.IsSuccess);
        Assert.Equal(1, version.Value.VersionNumber);
        Assert.Single(material.DomainEvents.OfType<LectureMaterialPublished>());
    }

    [Fact]
    public void A_subsequent_version_raises_LectureMaterialVersionPublished_not_the_first_publish_event()
    {
        var material = Published();
        material.ClearDomainEvents();

        var version = material.PublishNewVersion(Guid.NewGuid(), null, "Fixed a typo on slide 4.", Guid.NewGuid(), Now.AddDays(30));

        Assert.Equal(2, version.Value.VersionNumber);
        Assert.Single(material.DomainEvents.OfType<LectureMaterialVersionPublished>());
        Assert.Empty(material.DomainEvents.OfType<LectureMaterialPublished>());
    }

    /// <summary>requirement-spec.md §4: "LectureMaterialVersions are never deleted or mutated once published" - the prior version's own artifact reference is untouched by a later publish.</summary>
    [Fact]
    public void Publishing_a_new_version_never_mutates_or_removes_a_prior_one()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Lecture 1", null);
        var firstArtifact = Guid.NewGuid();
        var first = material.PublishNewVersion(firstArtifact, null, null, Guid.NewGuid(), Now).Value;

        var secondArtifact = Guid.NewGuid();
        var second = material.PublishNewVersion(secondArtifact, null, "Added a section.", Guid.NewGuid(), Now.AddDays(30)).Value;

        Assert.Equal(2, material.Versions.Count);
        Assert.Equal(firstArtifact, material.VersionById(first.Id)!.ArtifactId);
        Assert.Equal(secondArtifact, material.VersionById(second.Id)!.ArtifactId);
    }

    [Fact]
    public void Default_retrieval_resolves_to_the_latest_version()
    {
        var material = Published();
        var second = material.PublishNewVersion(Guid.NewGuid(), null, null, Guid.NewGuid(), Now.AddDays(30)).Value;

        Assert.Equal(second.Id, material.CurrentVersionId);
        Assert.Equal(second.Id, material.CurrentVersion!.Id);
    }

    [Fact]
    public void A_prior_version_stays_individually_addressable_by_its_own_id()
    {
        var material = Published();
        var first = material.CurrentVersion!;
        material.PublishNewVersion(Guid.NewGuid(), null, null, Guid.NewGuid(), Now.AddDays(30));

        var resolved = material.VersionById(first.Id);

        Assert.NotNull(resolved);
        Assert.Equal(1, resolved!.VersionNumber);
    }

    [Fact]
    public void A_version_referencing_both_an_artifact_and_a_URL_is_rejected()
    {
        var material = Create();

        var version = material.PublishNewVersion(Guid.NewGuid(), "https://example.edu.bd/lecture", null, Guid.NewGuid(), Now);

        Assert.True(version.IsFailure);
        Assert.Equal("lecture_material_version.exactly_one_source_required", version.Error!.Code);
    }

    [Fact]
    public void A_version_referencing_neither_an_artifact_nor_a_URL_is_rejected()
    {
        var material = Create();

        var version = material.PublishNewVersion(null, null, null, Guid.NewGuid(), Now);

        Assert.True(version.IsFailure);
        Assert.Equal("lecture_material_version.exactly_one_source_required", version.Error!.Code);
    }

    [Fact]
    public void A_Link_material_requires_an_external_URL_not_an_artifact()
    {
        var material = Create(LectureMaterialType.Link);

        var version = material.PublishNewVersion(Guid.NewGuid(), null, null, Guid.NewGuid(), Now);

        Assert.True(version.IsFailure);
        Assert.Equal("lecture_material_version.link_requires_url", version.Error!.Code);
    }

    [Fact]
    public void A_Video_material_requires_an_artifact_not_an_external_URL()
    {
        var material = Create(LectureMaterialType.Video);

        var version = material.PublishNewVersion(null, "https://example.edu.bd/video", null, Guid.NewGuid(), Now);

        Assert.True(version.IsFailure);
        Assert.Equal("lecture_material_version.file_requires_artifact", version.Error!.Code);
    }

    [Fact]
    public void A_blank_title_for_a_language_is_rejected()
    {
        var material = Create();

        var result = material.SetTranslation(LanguageCode.En, "   ", null);

        Assert.True(result.IsFailure);
        Assert.Equal("lecture_material.title_required", result.Error!.Code);
    }

    /// <summary>ums-conventions.md, Localization Implementation: English is the universal fallback, resolved server-side.</summary>
    [Fact]
    public void A_missing_Bengali_translation_falls_back_to_English()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Recursion", "An English description.");

        var resolved = material.ResolveTranslation(LanguageCode.Bn);

        Assert.Equal("Recursion", resolved!.Title);
        Assert.Equal(LanguageCode.En, resolved.Language);
    }

    [Fact]
    public void A_present_Bengali_translation_is_returned_in_preference_to_English()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Recursion", null);
        material.SetTranslation(LanguageCode.Bn, "রিকার্শন", null);

        var resolved = material.ResolveTranslation(LanguageCode.Bn);

        Assert.Equal("রিকার্শন", resolved!.Title);
        Assert.Equal(LanguageCode.Bn, resolved.Language);
    }

    [Fact]
    public void Re_setting_a_language_updates_it_in_place_rather_than_duplicating()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Recursion", null);

        material.SetTranslation(LanguageCode.En, "Recursion and Induction", "Now with induction.");

        Assert.Single(material.Translations);
        Assert.Equal("Recursion and Induction", material.ResolveTranslation(LanguageCode.En)!.Title);
    }

    private static LectureMaterial Create(LectureMaterialType type = LectureMaterialType.Document) =>
        LectureMaterial.Create(Guid.NewGuid(), type, "Week 3", 1, Guid.NewGuid(), Now).Value;

    private static LectureMaterial Published()
    {
        var material = Create();
        material.SetTranslation(LanguageCode.En, "Lecture 1", null);
        material.PublishNewVersion(Guid.NewGuid(), null, null, Guid.NewGuid(), Now);
        return material;
    }
}
