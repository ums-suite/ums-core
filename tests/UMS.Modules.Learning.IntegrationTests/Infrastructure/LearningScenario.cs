using System.Collections.Concurrent;

namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>
/// One fully-seeded course context (Organization chain, CourseOffering, assigned Instructor, two
/// enrolled Students) plus the occasional extras some tests need, memoized per test class.
///
/// <para>
/// <b>Why memoized rather than seeded per test:</b> every seeded actor is a real Identity User who
/// really logs in, and Identity's own <c>identity-login</c> per-IP rate limit (IDN-17, 100/min) is
/// applied to this suite exactly as it is in production - a per-test seed made this suite fail
/// against its OWN rate limiter with <c>503</c>s, which is the rate limiter behaving correctly, not
/// a defect. Seeding once per test class keeps the suite well inside that limit while still giving
/// each class its own isolated CourseOffering. Safe because every class here shares one xUnit
/// collection and therefore never runs in parallel.
/// </para>
/// </summary>
internal sealed class LearningScenario
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<LearningScenario>>> Cache = new(StringComparer.Ordinal);

    private readonly LearningApiFixture _fixture;

    private string? _unrelatedInstructorToken;
    private string? _unenrolledStudentToken;

    private LearningScenario(LearningApiFixture fixture, HttpClient client, string adminToken, LearningTestDataSeeder.CourseContext context)
    {
        _fixture = fixture;
        Client = client;
        AdminToken = adminToken;
        Context = context;
    }

    public HttpClient Client { get; }

    public string AdminToken { get; }

    public LearningTestDataSeeder.CourseContext Context { get; }

    public static Task<LearningScenario> GetAsync(LearningApiFixture fixture, string key) =>
        Cache.GetOrAdd(key, _ => new Lazy<Task<LearningScenario>>(() => CreateAsync(fixture))).Value;

    /// <summary>A real, login-capable Instructor of an entirely different Department - what every "another Instructor may not touch my resource" ownership test is driven with. No lock needed: every class here shares one xUnit collection, so nothing in this suite runs in parallel.</summary>
    public async Task<string> UnrelatedInstructorTokenAsync() =>
        _unrelatedInstructorToken ??= await LearningTestDataSeeder.SeedUnrelatedInstructorTokenAsync(_fixture, Client, AdminToken);

    /// <summary>A real, login-capable Active Student who is NOT enrolled in this scenario's CourseOffering - what every enrollment-scoping test is driven with.</summary>
    public async Task<string> UnenrolledStudentTokenAsync() =>
        _unenrolledStudentToken ??= await LearningTestDataSeeder.SeedUnenrolledStudentTokenAsync(_fixture, Client, Context.DepartmentId, Context.ProgramId);

    private static async Task<LearningScenario> CreateAsync(LearningApiFixture fixture)
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await LearningTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var context = await LearningTestDataSeeder.SeedCourseContextAsync(fixture, client, adminToken);
        return new LearningScenario(fixture, client, adminToken, context);
    }
}
