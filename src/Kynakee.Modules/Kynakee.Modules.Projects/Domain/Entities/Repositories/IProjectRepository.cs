using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.ValueObjects;

namespace Kynakee.Modules.Projects.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdWithFullStateAsync(
        ProjectId projectId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Project project,
        CancellationToken cancellationToken);
}
