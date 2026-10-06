using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.DataContext
{
    public sealed class ProjectContext : BaseEntity<ProjectContextId>
    {
        private ProjectContext()
        {
        }

        private ProjectContext(
            ProjectContextId id,
            Guid tenantId,
            ProjectId projectId,
            TerritorialContext territorial,
            NormativeContext normative,
            LaborContext labor,
            EconomicContext economic,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;
            Territorial = territorial;
            Normative = normative;
            Labor = labor;
            Economic = economic;
        }

        public ProjectId ProjectId { get; private set; }

        public TerritorialContext Territorial { get; private set; } = default!;

        public NormativeContext Normative { get; private set; } = default!;

        public LaborContext Labor { get; private set; } = default!;

        public EconomicContext Economic { get; private set; } = default!;

        public static Result<ProjectContext> Create(
            Guid tenantId,
            ProjectId projectId,
            TerritorialContext territorial,
            NormativeContext normative,
            LaborContext labor,
            EconomicContext economic,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<ProjectContext>(
                    ApplicationError.Validation(
                        "PROJ_CONTEXT_TENANT_REQUIRED",
                        "The project context tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<ProjectContext>(
                    ApplicationError.Validation(
                        "PROJ_CONTEXT_PROJECT_REQUIRED",
                        "The context project identifier is required."));
            }

            ArgumentNullException.ThrowIfNull(territorial);
            ArgumentNullException.ThrowIfNull(normative);
            ArgumentNullException.ThrowIfNull(labor);
            ArgumentNullException.ThrowIfNull(economic);

            return ResultFactory.Success(
                new ProjectContext(
                    ProjectContextId.New(),
                    tenantId,
                    projectId,
                    territorial,
                    normative,
                    labor,
                    economic,
                    createdBy));
        }
    }
}
