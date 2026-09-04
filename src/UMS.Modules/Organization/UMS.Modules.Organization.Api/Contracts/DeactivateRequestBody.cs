namespace UMS.Modules.Organization.Api.Contracts;

/// <summary>Shared body shape for every `POST /{entity}/{id}/deactivate` endpoint - `Version` is required per design-decisions.md's optimistic-concurrency decision.</summary>
public sealed record DeactivateRequestBody(uint Version);
