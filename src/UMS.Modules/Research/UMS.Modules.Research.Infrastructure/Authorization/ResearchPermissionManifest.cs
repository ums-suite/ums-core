using UMS.Modules.Research.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Research.Infrastructure.Authorization;

internal sealed class ResearchPermissionManifest : IPermissionManifest
{
    public string OwningModule => "research";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(ResearchPermissions.FundingBodyManage, "Create/edit FundingBody reference data (Admin/Research-Office)."),
        new(ResearchPermissions.GrantPropose, "Propose a new Grant (PI self-service or Admin/Research-Office)."),
        new(ResearchPermissions.GrantManage, "Fund/report/reject a Grant, and activate/close alongside the owning PI (Research-Office/Admin)."),
        new(ResearchPermissions.GrantInvestigatorsManage, "Add/remove a Grant's Co-Investigators or reassign its Principal Investigator (Research-Office/Admin, alongside the owning PI)."),
        new(ResearchPermissions.GrantRead, "Read Grant records (ownership/role-scoped for a PI/Co-I, unrestricted for Admin)."),
        new(ResearchPermissions.PublicationCreate, "Record a new Publication (any listed internal author or Admin)."),
        new(ResearchPermissions.PublicationUpdate, "Edit an existing Publication (any listed internal author or Admin)."),
        new(ResearchPermissions.PublicationMerge, "Resolve a PublicationDuplicateCandidate by merging two Publications (Admin only)."),
        new(ResearchPermissions.PublicationRead, "Read Publication records."),
        new(ResearchPermissions.RepositoryDeposit, "Deposit a new InstitutionalRepositoryEntry (depositing FacultyMember/advisor or Admin)."),
        new(ResearchPermissions.RepositoryManage, "Manually lift an InstitutionalRepositoryEntry's embargo early (Admin only)."),
        new(ResearchPermissions.RepositoryRead, "Read InstitutionalRepositoryEntry records (embargoed entries visible only to the depositor/advisor/Admin)."),
    ];
}
