namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/>'s implementation when persisting a new
/// <see cref="Domain.Users.User"/> violates a unique-identifier constraint - the losing side of
/// the race in edge-cases.md, "Concurrent provisioning creates a duplicate User for the same
/// person". The Application layer never touches the underlying database exception type;
/// Infrastructure translates it into this instead, so <c>UserProvisioningService</c> stays free
/// of any persistence-technology dependency.
/// </summary>
public sealed class DuplicateUserException(string identifierType, string identifierValue)
    : Exception($"A User already exists with {identifierType} '{identifierValue}'.")
{
    public string IdentifierType { get; } = identifierType;

    public string IdentifierValue { get; } = identifierValue;
}
