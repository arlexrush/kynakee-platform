using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Repositories;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Projects.Infrastructure.Repositories;

public sealed class EfProjectRepository : IProjectRepository
{
    private readonly ProjectsDbContext _dbContext;

    public EfProjectRepository(ProjectsDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Project?> GetByIdWithFullStateAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Projects
            .AsTracking()
            .AsSplitQuery()
            .Include(project => project.Capture)
                .ThenInclude(capture => capture!.MediaFiles)
            .Include(project => project.Capture)
                .ThenInclude(capture => capture!.Measurements)
            .Include(project => project.Capture)
                .ThenInclude(capture => capture!.Transcriptions)
            .Include(project => project.Capture)
                .ThenInclude(capture => capture!.Observations)
            .Include(project => project.Context)
            .Include(project => project.WorkItems)
            .Include(project => project.APUAssignments)
                .ThenInclude(assignment => assignment.Components)
                    .ThenInclude(component => component.PricingHistory)
            .Include(project => project.Schedule)
                .ThenInclude(schedule => schedule!.Activities)
            .Include(project => project.Schedule)
                .ThenInclude(schedule => schedule!.Precedences)
            .Include(project => project.Schedule)
                .ThenInclude(schedule => schedule!.Milestones)
            .Include(project => project.Valuation)
            .Include(project => project.Review)
                .ThenInclude(review => review!.Changes)
            .Include(project => project.Offer)
            .SingleOrDefaultAsync(
                project =>
                    project.Id == projectId &&
                    project.TenantId == _dbContext.CurrentTenantId,
                cancellationToken);
    }

    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        await _dbContext.Projects
            .AddAsync(project, cancellationToken)
            .ConfigureAwait(false);
    }
}
