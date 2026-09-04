using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Student.IntegrationTests.Infrastructure;
using UMS.Shared.Student;

namespace UMS.Modules.Student.IntegrationTests.Students;

/// <summary>
/// STU-1 concurrency-sensitive invariants (ums-conventions.md, Testing: "concurrency-sensitive
/// invariants require an explicit concurrent-access test, not just a happy-path unit test") -
/// edge-cases.md's "Admission's confirmation call to CreateStudentRecord is retried" and
/// "StudentNumber generation collides under concurrent creation load".
/// </summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class CreateStudentRecordConcurrencyTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Concurrent_CreateStudentRecord_calls_with_the_same_OriginatingApplicationId_create_exactly_one_Student()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var originatingApplicationId = Guid.NewGuid();

        var command = new CreateStudentRecordCommand(
            originatingApplicationId,
            2027,
            "EEE",
            departmentId,
            Guid.NewGuid(),
            "Karim",
            "Hossain",
            null,
            null,
            $"karim-{Guid.NewGuid():N}@example.edu.bd",
            null,
            new DateOnly(2006, 5, 5),
            "9876543210");

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = fixture.Services.CreateScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<IStudentRecordProvisioner>();
            return await provisioner.CreateAsync(command);
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        var distinctStudentIds = results.Select(r => r.Value.StudentId).Distinct().ToList();
        var distinctStudentNumbers = results.Select(r => r.Value.StudentNumber).Distinct().ToList();
        Assert.Single(distinctStudentIds);
        Assert.Single(distinctStudentNumbers);
    }

    [Fact]
    public async Task Concurrent_CreateStudentRecord_calls_for_different_applications_in_the_same_admissionYear_and_facultyCode_all_get_distinct_StudentNumbers()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var facultyCode = "BBA";
        var admissionYear = 2028;

        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            using var scope = fixture.Services.CreateScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<IStudentRecordProvisioner>();
            var command = new CreateStudentRecordCommand(
                Guid.NewGuid(),
                admissionYear,
                facultyCode,
                departmentId,
                Guid.NewGuid(),
                $"Student{i}",
                "Test",
                null,
                null,
                $"student{i}-{Guid.NewGuid():N}@example.edu.bd",
                null,
                new DateOnly(2005, 1, 1),
                null);
            return await provisioner.CreateAsync(command);
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        var studentNumbers = results.Select(r => r.Value.StudentNumber).ToList();
        Assert.Equal(20, studentNumbers.Distinct().Count());
    }
}
