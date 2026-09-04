using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.CourseAssignments;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.IntegrationTests.Infrastructure;

namespace UMS.Modules.Faculty.IntegrationTests.CourseAssignments;

/// <summary>
/// FAC-4: exercises <see cref="AcademicOutboxEventSource"/> (Infrastructure) and
/// <see cref="CourseAssignmentProjectionService"/> against a real Postgres table this test fabricates
/// to stand in for Academic's own future `academic.outbox_messages` (Academic - release/
/// DEVELOPMENT_PLAN.md Flow #12 - does not exist yet, see that class's own remarks for why this is
/// the honest way to verify the mechanism today). Calls the two services directly rather than
/// letting <c>CourseAssignmentProjectionRelayWorker</c>'s own polling loop run, so the test isn't
/// racing a 5-second timer.
/// </summary>
[Collection(FacultyApiTestCollectionDefinition.Name)]
public sealed class CourseAssignmentProjectionTests(FacultyApiFixture fixture)
{
    [Fact]
    public async Task Applying_InstructorAssigned_then_InstructorUnassigned_updates_the_projection_and_is_idempotent()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var member = await TestUsers.ProvisionAsync(client);
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var facultyMember = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        var courseOfferingId = Guid.NewGuid();
        await EnsureFabricatedAcademicOutboxTableAsync();

        var assignedEventId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        await InsertFabricatedEventAsync(assignedEventId, "InstructorAssigned", facultyMember!.Id, courseOfferingId, baseTime);

        using (var scope = fixture.Services.CreateScope())
        {
            var eventSource = scope.ServiceProvider.GetRequiredService<IInstructorAssignmentEventSource>();
            var projection = scope.ServiceProvider.GetRequiredService<CourseAssignmentProjectionService>();

            var envelopes = await eventSource.GetUnprocessedAsync(10);
            Assert.Contains(envelopes, e => e.EventId == assignedEventId);

            var envelope = envelopes.Single(e => e.EventId == assignedEventId);
            var payload = JsonSerializer.Deserialize<InstructorAssignmentPayload>(envelope.PayloadJson)!;
            var applyResult = await projection.ApplyInstructorAssignedAsync(payload, envelope.OccurredAt, envelope.EventId.ToString());
            Assert.True(applyResult.IsSuccess);
            await eventSource.MarkProcessedAsync(assignedEventId);
        }

        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/faculty/course-assignments?facultyMemberId={facultyMember.Id}").WithBearerToken(adminToken);
        var assignments = await (await client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<CourseAssignmentDto>>();
        Assert.Single(assignments!);
        Assert.Equal("Active", assignments![0].Status);

        // A duplicate delivery of the SAME event id must never be re-applied - it was already
        // marked processed above, so a second poll should not return it at all.
        using (var scope = fixture.Services.CreateScope())
        {
            var eventSource = scope.ServiceProvider.GetRequiredService<IInstructorAssignmentEventSource>();
            var envelopesAfterAck = await eventSource.GetUnprocessedAsync(10);
            Assert.DoesNotContain(envelopesAfterAck, e => e.EventId == assignedEventId);
        }

        // Now unassign - a later event.
        var unassignedEventId = Guid.NewGuid();
        await InsertFabricatedEventAsync(unassignedEventId, "InstructorUnassigned", facultyMember.Id, courseOfferingId, baseTime.AddMinutes(1));

        using (var scope = fixture.Services.CreateScope())
        {
            var eventSource = scope.ServiceProvider.GetRequiredService<IInstructorAssignmentEventSource>();
            var projection = scope.ServiceProvider.GetRequiredService<CourseAssignmentProjectionService>();

            var envelope = (await eventSource.GetUnprocessedAsync(10)).Single(e => e.EventId == unassignedEventId);
            var payload = JsonSerializer.Deserialize<InstructorAssignmentPayload>(envelope.PayloadJson)!;
            await projection.ApplyInstructorUnassignedAsync(payload, envelope.OccurredAt, envelope.EventId.ToString());
            await eventSource.MarkProcessedAsync(unassignedEventId);
        }

        var afterUnassign = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/faculty/course-assignments?facultyMemberId={facultyMember.Id}").WithBearerToken(adminToken))).Content.ReadFromJsonAsync<List<CourseAssignmentDto>>();
        Assert.Equal("Ended", afterUnassign![0].Status);
    }

    [Fact]
    public async Task Out_of_order_delivery_is_ignored_by_the_monotonic_ordering_guard()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var member = await TestUsers.ProvisionAsync(client);
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var facultyMember = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();
        var courseOfferingId = Guid.NewGuid();
        await EnsureFabricatedAcademicOutboxTableAsync();

        var now = DateTimeOffset.UtcNow;

        using (var scope = fixture.Services.CreateScope())
        {
            var projection = scope.ServiceProvider.GetRequiredService<CourseAssignmentProjectionService>();
            var payload = new InstructorAssignmentPayload(facultyMember!.Id, courseOfferingId);

            // Apply the NEWER assign first, then feed the OLDER (already-superseded) unassign -
            // simulating out-of-order at-least-once delivery (edge-cases.md).
            await projection.ApplyInstructorAssignedAsync(payload, now.AddMinutes(5), Guid.NewGuid().ToString());
            var staleResult = await projection.ApplyInstructorUnassignedAsync(payload, now, Guid.NewGuid().ToString());
            Assert.True(staleResult.IsSuccess);
        }

        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/faculty/course-assignments?facultyMemberId={facultyMember!.Id}").WithBearerToken(adminToken);
        var assignments = await (await client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<CourseAssignmentDto>>();

        // The stale Unassigned must NOT have overwritten the newer Active state.
        Assert.Single(assignments!);
        Assert.Equal("Active", assignments![0].Status);
    }

    private async Task EnsureFabricatedAcademicOutboxTableAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE SCHEMA IF NOT EXISTS academic;
            CREATE TABLE IF NOT EXISTS academic.outbox_messages (
                id uuid PRIMARY KEY,
                event_type text NOT NULL,
                payload_json text NOT NULL,
                occurred_at timestamptz NOT NULL,
                recorded_at timestamptz NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertFabricatedEventAsync(Guid eventId, string eventType, Guid facultyMemberId, Guid courseOfferingId, DateTimeOffset occurredAt)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO academic.outbox_messages (id, event_type, payload_json, occurred_at, recorded_at) VALUES (@id, @eventType, @payload, @occurredAt, @recordedAt)";
        command.Parameters.AddWithValue("id", eventId);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(new InstructorAssignmentPayload(facultyMemberId, courseOfferingId)));
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        command.Parameters.AddWithValue("recordedAt", occurredAt);
        await command.ExecuteNonQueryAsync();
    }
}
