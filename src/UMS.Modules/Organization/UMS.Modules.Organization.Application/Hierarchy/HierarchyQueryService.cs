using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Shared.ErrorHandling.Results;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.Application.Hierarchy;

/// <summary>
/// ORG-9/ORG-10: `GET /organization-tree` (full or subtree) and `GET /nodes/{id}/ancestors`
/// (requirement-spec.md organization §2/§6). Cache-aside against Redis (design-decisions.md,
/// "Caching Strategy for the Hierarchy Tree") - every read here tries <see cref="IOrganizationTreeCache"/>
/// first; a miss rebuilds from Postgres (a handful of small-table reads, §2's own scale reference)
/// and re-populates the cache before returning.
/// </summary>
public sealed class HierarchyQueryService(
    IUniversityRepository universities,
    ICampusRepository campuses,
    IFacultyRepository faculties,
    IDepartmentRepository departments,
    IProgramRepository programs,
    IOrganizationTreeCache treeCache)
{
    private static readonly JsonSerializerOptions _serializerOptions = new();

    public async Task<Result<IReadOnlyList<OrganizationTreeNodeDto>>> GetTreeAsync(Guid? rootId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var cached = await treeCache.GetTreeJsonAsync(rootId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<OrganizationTreeNodeDto>>(cached, _serializerOptions) ?? [];
        }

        var fullTree = await BuildFullTreeAsync(languageCode, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OrganizationTreeNodeDto> result;
        if (rootId is null)
        {
            result = fullTree;
        }
        else
        {
            var node = FindNode(fullTree, rootId.Value);
            if (node is null)
            {
                return Error.NotFound("organization_node.not_found", $"No organization hierarchy node exists with id '{rootId}'.");
            }

            result = [node];
        }

        await treeCache.SetTreeJsonAsync(rootId, JsonSerializer.Serialize(result, _serializerOptions), cancellationToken).ConfigureAwait(false);
        return Result.Success(result);
    }

    public async Task<Result<IReadOnlyList<AncestorNodeDto>>> GetAncestorsAsync(Guid nodeId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var cached = await treeCache.GetAncestorsJsonAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<AncestorNodeDto>>(cached, _serializerOptions) ?? [];
        }

        var chain = await ResolveAncestorChainAsync(nodeId, languageCode, cancellationToken).ConfigureAwait(false);
        if (chain is null)
        {
            return Error.NotFound("organization_node.not_found", $"No organization hierarchy node exists with id '{nodeId}'.");
        }

        await treeCache.SetAncestorsJsonAsync(nodeId, JsonSerializer.Serialize(chain, _serializerOptions), cancellationToken).ConfigureAwait(false);
        return chain;
    }

    private static OrganizationTreeNodeDto? FindNode(IReadOnlyList<OrganizationTreeNodeDto> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            var found = FindNode(node.Children, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async Task<List<AncestorNodeDto>?> ResolveAncestorChainAsync(Guid nodeId, string? languageCode, CancellationToken cancellationToken)
    {
        var program = await programs.GetByIdAsync(new OrgProgramId(nodeId), cancellationToken).ConfigureAwait(false);
        if (program is not null)
        {
            var chain = await ResolveAncestorChainAsync(program.DepartmentId.Value, languageCode, cancellationToken).ConfigureAwait(false);
            chain?.Add(new AncestorNodeDto(program.Id.Value, "Program", program.ResolveName(languageCode)));
            return chain;
        }

        var department = await departments.GetByIdAsync(new DepartmentId(nodeId), cancellationToken).ConfigureAwait(false);
        if (department is not null)
        {
            var chain = await ResolveAncestorChainAsync(department.FacultyId.Value, languageCode, cancellationToken).ConfigureAwait(false);
            chain?.Add(new AncestorNodeDto(department.Id.Value, nameof(Department), department.ResolveName(languageCode)));
            return chain;
        }

        var faculty = await faculties.GetByIdAsync(new FacultyId(nodeId), cancellationToken).ConfigureAwait(false);
        if (faculty is not null)
        {
            var chain = await ResolveAncestorChainAsync(faculty.CampusId.Value, languageCode, cancellationToken).ConfigureAwait(false);
            chain?.Add(new AncestorNodeDto(faculty.Id.Value, nameof(Faculty), faculty.ResolveName(languageCode)));
            return chain;
        }

        var campus = await campuses.GetByIdAsync(new CampusId(nodeId), cancellationToken).ConfigureAwait(false);
        if (campus is not null)
        {
            var chain = await ResolveAncestorChainAsync(campus.UniversityId.Value, languageCode, cancellationToken).ConfigureAwait(false);
            chain?.Add(new AncestorNodeDto(campus.Id.Value, nameof(Campus), campus.Name));
            return chain;
        }

        var university = await universities.GetByIdAsync(new UniversityId(nodeId), cancellationToken).ConfigureAwait(false);
        return university is not null
            ? [new AncestorNodeDto(university.Id.Value, nameof(University), university.Name)]
            : null;
    }

    private async Task<List<OrganizationTreeNodeDto>> BuildFullTreeAsync(string? languageCode, CancellationToken cancellationToken)
    {
        var universityList = await universities.ListAsync(0, int.MaxValue, cancellationToken).ConfigureAwait(false);
        var campusList = await campuses.ListAsync(null, 0, int.MaxValue, cancellationToken).ConfigureAwait(false);
        var facultyList = await faculties.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var departmentList = await departments.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var programList = await programs.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var programsByDepartment = programList.ToLookup(p => p.DepartmentId);
        var departmentsByFaculty = departmentList.ToLookup(d => d.FacultyId);
        var facultiesByCampus = facultyList.ToLookup(f => f.CampusId);
        var campusesByUniversity = campusList.ToLookup(c => c.UniversityId);

        return universityList.Select(university =>
        {
            var campusNodes = campusesByUniversity[university.Id].Select(campus =>
            {
                var facultyNodes = facultiesByCampus[campus.Id].Select(faculty =>
                {
                    var departmentNodes = departmentsByFaculty[faculty.Id].Select(department =>
                    {
                        var programNodes = programsByDepartment[department.Id]
                            .Select(program => new OrganizationTreeNodeDto(program.Id.Value, "Program", program.ResolveName(languageCode), program.Status.ToString(), []))
                            .ToList();
                        return new OrganizationTreeNodeDto(department.Id.Value, nameof(Department), department.ResolveName(languageCode), department.Status.ToString(), programNodes);
                    }).ToList();
                    return new OrganizationTreeNodeDto(faculty.Id.Value, nameof(Faculty), faculty.ResolveName(languageCode), faculty.Status.ToString(), departmentNodes);
                }).ToList();
                return new OrganizationTreeNodeDto(campus.Id.Value, nameof(Campus), campus.Name, campus.Status.ToString(), facultyNodes);
            }).ToList();
            return new OrganizationTreeNodeDto(university.Id.Value, nameof(University), university.Name, university.Status.ToString(), campusNodes);
        }).ToList();
    }
}
