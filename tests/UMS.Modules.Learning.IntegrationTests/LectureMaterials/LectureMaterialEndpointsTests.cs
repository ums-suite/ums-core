using System.Net;
using UMS.Modules.Learning.Application.LectureMaterials;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.LectureMaterials;

/// <summary>LRN-12/LRN-13/LRN-14/LRN-15: publication, the shared Documents presigned-upload flow, append-only versioning, and enrollment-scoped browse.</summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class LectureMaterialEndpointsTests(LearningApiFixture fixture)
{
    [Fact]
    public async Task An_Instructor_publishes_a_link_material_with_bilingual_metadata()
    {
        var (client, context) = await SeedAsync();
        var group = UniqueGroup();

        var material = await PublishLinkAsync(client, context, group, 0);

        Assert.Equal("Link", material.MaterialType);
        Assert.Equal(group, material.ModuleGroup);
        Assert.Equal("Introduction to Recursion", material.Title);
        Assert.Equal(1, material.CurrentVersion!.VersionNumber);
    }

    /// <summary>ums-conventions.md's Localization Implementation: resolved server-side against <c>?lang=</c>, with English as the universal fallback.</summary>
    [Fact]
    public async Task Browsing_resolves_the_requested_language_and_falls_back_to_English()
    {
        var (client, context) = await SeedAsync();
        var bilingualGroup = UniqueGroup();
        var englishOnlyGroup = UniqueGroup();
        await PublishLinkAsync(client, context, bilingualGroup, 0);
        await client.PostAndReadAsync<LectureMaterialDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
            context.InstructorToken,
            new CreateLectureMaterialRequest("Link", englishOnlyGroup, 0, "English only", null, null, null, null, "https://example.edu.bd/w2"));

        var bengali = await client.GetAndReadAsync<List<LectureMaterialDto>>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials?lang=bn",
            context.StudentToken);

        var translated = bengali.Single(m => m.ModuleGroup == bilingualGroup);
        var fallback = bengali.Single(m => m.ModuleGroup == englishOnlyGroup);

        Assert.Equal("রিকার্শনের ভূমিকা", translated.Title);
        Assert.Equal("Bn", translated.ResolvedLanguage);
        Assert.Equal("English only", fallback.Title);
        Assert.Equal("En", fallback.ResolvedLanguage);
    }

    [Fact]
    public async Task Materials_are_returned_ordered_by_module_group_then_sort_order()
    {
        var (client, context) = await SeedAsync();
        var prefix = $"Ordering-{Guid.NewGuid():N}";
        await PublishLinkAsync(client, context, $"{prefix}-B", 1);
        await PublishLinkAsync(client, context, $"{prefix}-A", 1);
        await PublishLinkAsync(client, context, $"{prefix}-A", 0);

        var materials = (await client.GetAndReadAsync<List<LectureMaterialDto>>(
                $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
                context.StudentToken))
            .Where(m => m.ModuleGroup.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        Assert.Equal([$"{prefix}-A", $"{prefix}-A", $"{prefix}-B"], materials.Select(m => m.ModuleGroup));
        Assert.Equal([0, 1, 1], materials.Select(m => m.SortOrder));
    }

    /// <summary>
    /// design-decisions.md "LectureMaterial Versioning &amp; Retrieval Default": default retrieval
    /// advances to the latest version while every prior version stays individually addressable by
    /// its own id, exactly as published.
    /// </summary>
    [Fact]
    public async Task Publishing_a_new_version_advances_the_default_while_the_old_version_stays_individually_addressable()
    {
        var (client, context) = await SeedAsync();
        var material = await PublishLinkAsync(client, context, UniqueGroup(), 0);
        var firstVersionId = material.CurrentVersion!.Id;
        var firstUrl = material.CurrentVersion.ExternalUrl;

        var updated = await client.PostAndReadAsync<LectureMaterialDto>(
            $"/api/v1/learning/lecture-materials/{material.Id}/versions",
            context.InstructorToken,
            new PublishLectureMaterialVersionRequest(null, "https://example.edu.bd/w3-v2", "Fixed a typo on slide 4."));

        Assert.Equal(2, updated.CurrentVersion!.VersionNumber);
        Assert.Equal("https://example.edu.bd/w3-v2", updated.CurrentVersion.ExternalUrl);
        Assert.Equal(2, updated.Versions.Count);

        var priorVersion = await client.GetAndReadAsync<LectureMaterialVersionDto>(
            $"/api/v1/learning/lecture-materials/{material.Id}/versions/{firstVersionId}",
            context.StudentToken);

        Assert.Equal(1, priorVersion.VersionNumber);
        Assert.Equal(firstUrl, priorVersion.ExternalUrl);
    }

    /// <summary>LRN-13 against Documents' REAL presigned-upload pipeline and real object storage - the same mechanism LRN-5 uses, not a second one.</summary>
    [Fact]
    public async Task A_video_material_is_uploaded_through_Documents_presigned_flow_and_referenced_by_artifactId()
    {
        var (client, context) = await SeedAsync();

        var slot = await client.PostAndReadAsync<LectureMaterialUploadSlotDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials/uploads",
            context.InstructorToken,
            new RequestLectureMaterialUploadRequest("video/mp4"));

        Assert.NotNull(slot.UploadUrl);

        using (var uploader = new HttpClient())
        {
            using var content = new ByteArrayContent([0x00, 0x01, 0x02, 0x03]);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
            (await uploader.PutAsync(new Uri(slot.UploadUrl!), content)).EnsureSuccessStatusCode();
        }

        var confirmed = await client.PostAndReadAsync<LectureMaterialUploadSlotDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials/uploads/confirm",
            context.InstructorToken,
            new ConfirmLectureMaterialUploadRequest(slot.ArtifactId));
        Assert.Equal("Ready", confirmed.Status);

        var material = await client.PostAndReadAsync<LectureMaterialDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
            context.InstructorToken,
            new CreateLectureMaterialRequest("Video", UniqueGroup(), 0, "Recorded lecture", null, null, null, slot.ArtifactId, null));

        Assert.Equal(slot.ArtifactId, material.CurrentVersion!.ArtifactId);
    }

    [Fact]
    public async Task An_Instructor_who_does_not_own_the_offering_cannot_publish_material_to_it()
    {
        var (client, context) = await SeedAsync();
        var otherInstructorToken = await (await ScenarioAsync()).UnrelatedInstructorTokenAsync();

        var response = await client.PostAsync(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
            otherInstructorToken,
            new CreateLectureMaterialRequest("Link", UniqueGroup(), 0, "Not mine", null, null, null, null, "https://example.edu.bd/w9"));

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "lecture_material.not_assigned_instructor");
    }

    [Fact]
    public async Task A_Student_who_is_not_enrolled_cannot_browse_the_offerings_materials()
    {
        var (client, context) = await SeedAsync();
        await PublishLinkAsync(client, context, UniqueGroup(), 0);
        var outsiderToken = await (await ScenarioAsync()).UnenrolledStudentTokenAsync();

        var response = await client.GetAsync(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
            outsiderToken);

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "learning.not_enrolled_or_instructor");
    }

    [Fact]
    public async Task A_version_referencing_neither_an_artifact_nor_a_URL_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var material = await PublishLinkAsync(client, context, UniqueGroup(), 0);

        var response = await client.PostAsync(
            $"/api/v1/learning/lecture-materials/{material.Id}/versions",
            context.InstructorToken,
            new PublishLectureMaterialVersionRequest(null, null, "Nothing attached."));

        await response.AssertProblemCodeAsync(HttpStatusCode.BadRequest, "lecture_material_version.exactly_one_source_required");
    }

    /// <summary>Each test gets its own syllabus grouping: the CourseOffering itself is shared across this class (see <see cref="LearningScenario"/>), so a fixed group name would let one test's materials leak into another's assertions.</summary>
    private static string UniqueGroup() => $"Week-{Guid.NewGuid():N}";

    private static Task<LectureMaterialDto> PublishLinkAsync(HttpClient client, LearningTestDataSeeder.CourseContext context, string moduleGroup, int sortOrder) =>
        client.PostAndReadAsync<LectureMaterialDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/lecture-materials",
            context.InstructorToken,
            new CreateLectureMaterialRequest(
                "Link",
                moduleGroup,
                sortOrder,
                "Introduction to Recursion",
                "Slides and reading list.",
                "রিকার্শনের ভূমিকা",
                "স্লাইড এবং পাঠ্যতালিকা।",
                null,
                $"https://example.edu.bd/{Guid.NewGuid():N}"));

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "lecture-materials");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "lecture-materials");
}
