using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Application.StudentRequests;
using UMS.Modules.Student.Domain.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.StudentRequests;

/// <summary>STU-9..STU-14 (requirement-spec.md student §2/§4/§6; design-decisions.md "StudentRequest Dedup Mechanism"; edge-cases.md's Suspended-student and own-Department-Head-escalation scenarios).</summary>
public sealed class StudentRequestServiceTests
{
    private static AuditContext Audit(Guid actorUserId) => new(actorUserId, "127.0.0.1", Guid.NewGuid().ToString());

    private static Domain.Students.Student CreateEnrolledStudent(Guid identityUserId, Guid departmentId)
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 1);
        var name = PersonName.Create("Rahim", "Uddin").Value;
        var email = Email.Create("rahim@example.edu.bd").Value;
        var student = Domain.Students.Student.Enroll(Guid.NewGuid(), studentNumber, departmentId, Guid.NewGuid(), name, email, mobile: null, new DateOnly(2005, 1, 1), "1234567890", DateTimeOffset.UtcNow);
        student.SetIdentityUser(identityUserId);
        return student;
    }

    private static (StudentRequestService Service, FakeStudentRepository Students, FakeStudentRequestRepository Requests, FakeReviewerScopeDirectory ScopeDirectory, FakeNotificationRequestPublisher Notifications) CreateService(Guid? parentFacultyId = null)
    {
        var students = new FakeStudentRepository();
        var requests = new FakeStudentRequestRepository();
        var scopeDirectory = new FakeReviewerScopeDirectory();
        var notifications = new FakeNotificationRequestPublisher();

        var service = new StudentRequestService(
            requests,
            students,
            new FakeDepartmentFacultyLookup(parentFacultyId),
            scopeDirectory,
            new FakeAcademicTranscriptPort(),
            new FakeDocumentGenerationPort(),
            notifications,
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock(),
            NullLogger<StudentRequestService>.Instance);

        return (service, students, requests, scopeDirectory, notifications);
    }

    [Fact]
    public async Task SubmitIdReissueAsync_for_a_Suspended_student_is_forbidden()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        student.ChangeStatus(StudentStatus.Suspended, "disciplinary", Guid.NewGuid(), DateTimeOffset.UtcNow);
        students.Seed(student);

        var result = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost my card"), Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.suspended_restricted", result.Error!.Code);
    }

    [Fact]
    public async Task SubmitTranscriptRequestAsync_for_a_Suspended_student_is_forbidden()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        student.ChangeStatus(StudentStatus.Suspended, "disciplinary", Guid.NewGuid(), DateTimeOffset.UtcNow);
        students.Seed(student);

        var result = await service.SubmitTranscriptRequestAsync(userId, new SubmitTranscriptRequestRequest("Job application"), Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.suspended_restricted", result.Error!.Code);
    }

    [Fact]
    public async Task SubmitGrievanceAsync_for_a_Suspended_student_still_succeeds()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        student.ChangeStatus(StudentStatus.Suspended, "disciplinary", Guid.NewGuid(), DateTimeOffset.UtcNow);
        students.Seed(student);

        var result = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Unfair treatment", false), Audit(userId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SubmitIdReissueAsync_when_an_open_request_of_the_same_type_exists_returns_the_fast_path_conflict()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);

        var first = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost my card"), Audit(userId));
        Assert.True(first.IsSuccess);

        var second = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost it again"), Audit(userId));

        Assert.True(second.IsFailure);
        Assert.Equal("studentrequest.duplicate_open_request", second.Error!.Code);
        Assert.Contains(first.Value.Id.ToString(), second.Error.Message);
    }

    [Fact]
    public async Task SubmitGrievanceAsync_routes_to_the_students_own_department_by_default()
    {
        var departmentId = Guid.NewGuid();
        var (service, students, requests, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);

        var result = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Noise complaint", IsAgainstOwnDepartmentHead: false), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Equal(departmentId, result.Value.ReviewScopeNodeId);
    }

    /// <summary>edge-cases.md "A grievance is filed against a Department Head's own department" - escalates one scope level up (to the parent Faculty).</summary>
    [Fact]
    public async Task SubmitGrievanceAsync_against_own_department_head_escalates_to_the_parent_Faculty()
    {
        var departmentId = Guid.NewGuid();
        var facultyId = Guid.NewGuid();
        var (service, students, requests, _, _) = CreateService(parentFacultyId: facultyId);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);

        var result = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("My Department Head retaliated against me", IsAgainstOwnDepartmentHead: true), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Equal(facultyId, result.Value.ReviewScopeNodeId);
        Assert.NotEqual(departmentId, result.Value.ReviewScopeNodeId);
    }

    [Fact]
    public async Task SubmitGrievanceAsync_against_own_department_head_with_no_parent_Faculty_falls_back_to_university_wide_scope()
    {
        var (service, students, _, _, _) = CreateService(parentFacultyId: null);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);

        var result = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", IsAgainstOwnDepartmentHead: true), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ReviewScopeNodeId);
    }

    [Fact]
    public async Task SubmitGrievanceAsync_notifies_reviewers_scoped_to_the_routed_department()
    {
        var departmentId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var (service, students, _, scopeDirectory, notifications) = CreateService();
        scopeDirectory.Grant(reviewerId, StudentPermissions.RequestReview, departmentId);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);

        await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", false), Audit(userId));

        Assert.Single(notifications.Published, n => n.RecipientUserId == reviewerId && n.EventType == "StudentRequestSubmitted");
    }

    [Fact]
    public async Task GetByIdAsync_the_owning_student_can_read_their_own_request()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);
        var submitted = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost card"), Audit(userId));

        var result = await service.GetByIdAsync(submitted.Value.Id, userId, callerHasReviewPermission: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetByIdAsync_an_unrelated_user_without_review_permission_is_forbidden()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);
        var submitted = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost card"), Audit(userId));

        var result = await service.GetByIdAsync(submitted.Value.Id, Guid.NewGuid(), callerHasReviewPermission: false);

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.access_forbidden", result.Error!.Code);
    }

    /// <summary>requirement-spec.md §5: "a Department Head must see only grievances scoped to their own department, never another's."</summary>
    [Fact]
    public async Task GetByIdAsync_a_reviewer_out_of_scope_for_the_routed_department_is_forbidden()
    {
        var departmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var (service, students, _, scopeDirectory, _) = CreateService();
        scopeDirectory.Grant(reviewerId, StudentPermissions.RequestReview, otherDepartmentId);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);
        var submitted = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", false), Audit(userId));

        var result = await service.GetByIdAsync(submitted.Value.Id, reviewerId, callerHasReviewPermission: true);

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.access_forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_a_reviewer_in_scope_for_the_routed_department_succeeds()
    {
        var departmentId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var (service, students, _, scopeDirectory, _) = CreateService();
        scopeDirectory.Grant(reviewerId, StudentPermissions.RequestReview, departmentId);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);
        var submitted = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", false), Audit(userId));

        var result = await service.GetByIdAsync(submitted.Value.Id, reviewerId, callerHasReviewPermission: true);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ApproveAsync_for_an_IdReissue_request_triggers_fulfillment_and_reaches_Fulfilled()
    {
        var (service, students, _, scopeDirectory, _) = CreateService();
        var userId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);
        var submitted = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost card"), Audit(userId));

        var result = await service.ApproveAsync(submitted.Value.Id, reviewerId, submitted.Value.Version, Audit(reviewerId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Fulfilled", result.Value.Status);
        Assert.NotNull(result.Value.GeneratedDocumentId);
    }

    [Fact]
    public async Task ApproveAsync_for_a_Grievance_reaches_Fulfilled_with_no_document()
    {
        var departmentId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var (service, students, _, scopeDirectory, _) = CreateService();
        scopeDirectory.Grant(reviewerId, StudentPermissions.RequestReview, departmentId);
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);
        var submitted = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", false), Audit(userId));

        var result = await service.ApproveAsync(submitted.Value.Id, reviewerId, submitted.Value.Version, Audit(reviewerId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Fulfilled", result.Value.Status);
        Assert.Null(result.Value.GeneratedDocumentId);
    }

    [Fact]
    public async Task ApproveAsync_by_a_reviewer_out_of_scope_is_forbidden_and_does_not_mutate_the_request()
    {
        var departmentId = Guid.NewGuid();
        var (service, students, requests, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, departmentId);
        students.Seed(student);
        var submitted = await service.SubmitGrievanceAsync(userId, new SubmitGrievanceRequest("Complaint", false), Audit(userId));

        var result = await service.ApproveAsync(submitted.Value.Id, Guid.NewGuid(), submitted.Value.Version, Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.review_forbidden", result.Error!.Code);
        var stillOpen = await requests.GetByIdAsync(new UMS.Modules.Student.Domain.StudentRequests.StudentRequestId(submitted.Value.Id));
        Assert.Equal(UMS.Modules.Student.Domain.StudentRequests.StudentRequestStatus.Submitted, stillOpen!.Status);
    }

    [Fact]
    public async Task RejectAsync_requires_a_non_blank_reason()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);
        var submitted = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost card"), Audit(userId));

        var result = await service.RejectAsync(submitted.Value.Id, Guid.NewGuid(), new RejectStudentRequestRequest(string.Empty, submitted.Value.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("studentrequest.reject_reason_required", result.Error!.Code);
    }

    [Fact]
    public async Task RejectAsync_with_a_reason_succeeds()
    {
        var (service, students, _, _, _) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudent(userId, Guid.NewGuid());
        students.Seed(student);
        var submitted = await service.SubmitIdReissueAsync(userId, new SubmitIdReissueRequest("Lost card"), Audit(userId));

        var result = await service.RejectAsync(submitted.Value.Id, Guid.NewGuid(), new RejectStudentRequestRequest("Not eligible", submitted.Value.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", result.Value.Status);
    }
}
