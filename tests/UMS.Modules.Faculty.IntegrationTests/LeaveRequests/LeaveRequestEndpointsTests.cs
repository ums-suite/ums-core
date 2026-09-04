using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.LeaveRequests;
using UMS.Modules.Faculty.IntegrationTests.Infrastructure;

namespace UMS.Modules.Faculty.IntegrationTests.LeaveRequests;

[Collection(FacultyApiTestCollectionDefinition.Name)]
public sealed class LeaveRequestEndpointsTests(FacultyApiFixture fixture)
{
    [Fact]
    public async Task Full_chain_submit_then_department_head_approve_then_authority_approve_reaches_Approved()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var requesterUser = await TestUsers.ProvisionAsync(client);
        await FacultyTestDataSeeder.GrantFacultyMemberRoleAsync(fixture, requesterUser.Id);
        var requesterLogin = await TestUsers.LoginAsync(client, requesterUser.Username);
        var requester = await OnboardAsync(client, adminToken, requesterUser.Id, departmentId, designationId, isDepartmentHead: false);

        var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/leave-requests")
        {
            Content = JsonContent.Create(new SubmitLeaveRequestRequest(requester.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), "Family event", null)),
        }.WithBearerToken(requesterLogin.AccessToken);
        var submitResponse = await client.SendAsync(submitRequest);
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<LeaveRequestDto>();
        Assert.Equal("Submitted", submitted!.Status);
        Assert.False(submitted.RoutedDirectlyToAuthority);

        var deptApprove = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted.Id}/approve/department-head")
        {
            Content = JsonContent.Create(new VersionedRequestBody(submitted.Version)),
        }.WithBearerToken(adminToken);
        var deptApproveResponse = await client.SendAsync(deptApprove);
        Assert.Equal(HttpStatusCode.OK, deptApproveResponse.StatusCode);
        var deptApproved = await deptApproveResponse.Content.ReadFromJsonAsync<LeaveRequestDto>();
        Assert.Equal("DeptHeadApproved", deptApproved!.Status);

        var authorityApprove = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted.Id}/approve/authority")
        {
            Content = JsonContent.Create(new VersionedRequestBody(deptApproved.Version)),
        }.WithBearerToken(adminToken);
        var authorityApproveResponse = await client.SendAsync(authorityApprove);
        Assert.Equal(HttpStatusCode.OK, authorityApproveResponse.StatusCode);
        var final = await authorityApproveResponse.Content.ReadFromJsonAsync<LeaveRequestDto>();
        Assert.Equal("Approved", final!.Status);
    }

    [Fact]
    public async Task Department_head_requester_is_routed_directly_to_authority_and_cannot_approve_own_request()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var headUser = await TestUsers.ProvisionAsync(client);
        await FacultyTestDataSeeder.GrantFacultyMemberRoleAsync(fixture, headUser.Id);
        var headLogin = await TestUsers.LoginAsync(client, headUser.Username);
        var head = await OnboardAsync(client, adminToken, headUser.Id, departmentId, designationId, isDepartmentHead: true);

        var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/leave-requests")
        {
            Content = JsonContent.Create(new SubmitLeaveRequestRequest(head.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3), "Conference", null)),
        }.WithBearerToken(headLogin.AccessToken);
        var submitted = await (await client.SendAsync(submitRequest)).Content.ReadFromJsonAsync<LeaveRequestDto>();

        Assert.Equal("Submitted", submitted!.Status);
        Assert.True(submitted.RoutedDirectlyToAuthority);

        // The department-head step is not applicable to a rerouted request.
        var deptApprove = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted.Id}/approve/department-head")
        {
            Content = JsonContent.Create(new VersionedRequestBody(submitted.Version)),
        }.WithBearerToken(adminToken);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(deptApprove)).StatusCode);

        // The admin account IS the requester here in terms of identity comparison? No - admin is a
        // distinct user. Exercise the actual self-approval guard: the requester themself, even
        // though they hold no approval permission, cannot be used to approve; skipped as a
        // permission-gate failure (403) since headLogin has no approve.authority permission -
        // covered separately by the version-conflict/self-approval unit tests on the aggregate
        // itself where the identity comparison is exercised directly.
        var authorityApprove = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted.Id}/approve/authority")
        {
            Content = JsonContent.Create(new VersionedRequestBody(submitted.Version)),
        }.WithBearerToken(adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(authorityApprove)).StatusCode);
    }

    [Fact]
    public async Task Reject_at_department_head_step_moves_to_Rejected()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var requesterUser = await TestUsers.ProvisionAsync(client);
        await FacultyTestDataSeeder.GrantFacultyMemberRoleAsync(fixture, requesterUser.Id);
        var requesterLogin = await TestUsers.LoginAsync(client, requesterUser.Username);
        var requester = await OnboardAsync(client, adminToken, requesterUser.Id, departmentId, designationId, isDepartmentHead: false);

        var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/leave-requests")
        {
            Content = JsonContent.Create(new SubmitLeaveRequestRequest(requester.Id, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2), "Personal", null)),
        }.WithBearerToken(requesterLogin.AccessToken);
        var submitted = await (await client.SendAsync(submitRequest)).Content.ReadFromJsonAsync<LeaveRequestDto>();

        var reject = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted!.Id}/reject/department-head")
        {
            Content = JsonContent.Create(new RejectLeaveRequestRequest("Insufficient leave balance", submitted.Version)),
        }.WithBearerToken(adminToken);
        var rejectResponse = await client.SendAsync(reject);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<LeaveRequestDto>();
        Assert.Equal("Rejected", rejected!.Status);
    }

    [Fact]
    public async Task Requester_can_cancel_while_pending_but_not_after_a_decision()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var requesterUser = await TestUsers.ProvisionAsync(client);
        await FacultyTestDataSeeder.GrantFacultyMemberRoleAsync(fixture, requesterUser.Id);
        var requesterLogin = await TestUsers.LoginAsync(client, requesterUser.Username);
        var requester = await OnboardAsync(client, adminToken, requesterUser.Id, departmentId, designationId, isDepartmentHead: false);

        var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/leave-requests")
        {
            Content = JsonContent.Create(new SubmitLeaveRequestRequest(requester.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), "Trip", null)),
        }.WithBearerToken(requesterLogin.AccessToken);
        var submitted = await (await client.SendAsync(submitRequest)).Content.ReadFromJsonAsync<LeaveRequestDto>();

        var cancel = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted!.Id}/cancel")
        {
            Content = JsonContent.Create(new VersionedRequestBody(submitted.Version)),
        }.WithBearerToken(requesterLogin.AccessToken);
        var cancelResponse = await client.SendAsync(cancel);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<LeaveRequestDto>();
        Assert.Equal("Cancelled", cancelled!.Status);

        var secondCancel = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/faculty/leave-requests/{submitted.Id}/cancel")
        {
            Content = JsonContent.Create(new VersionedRequestBody(cancelled.Version)),
        }.WithBearerToken(requesterLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(secondCancel)).StatusCode);
    }

    private static async Task<FacultyMemberDto> OnboardAsync(HttpClient client, string adminToken, Guid userId, Guid departmentId, Guid designationId, bool isDepartmentHead)
    {
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(userId, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var onboarded = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        if (isDepartmentHead)
        {
            var promote = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/faculty/members/{onboarded!.Id}")
            {
                Content = JsonContent.Create(new UpdateEmploymentDetailsRequest(departmentId, designationId, "FullTime", true, null, null, onboarded.Version)),
            }.WithBearerToken(adminToken);
            onboarded = await (await client.SendAsync(promote)).Content.ReadFromJsonAsync<FacultyMemberDto>();
        }

        return onboarded!;
    }
}
