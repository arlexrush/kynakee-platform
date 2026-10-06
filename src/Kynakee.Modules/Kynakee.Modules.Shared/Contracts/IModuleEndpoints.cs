using Microsoft.AspNetCore.Routing;

namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Contract for registering Minimal API endpoints in a module.
    /// Every module MUST implement this interface to expose its endpoints (Copilot Rule #12).
    ///
    /// All endpoints MUST use Minimal API style — never use Controllers (ADR-003).
    /// Endpoints are registered at application startup via module registration.
    ///
    /// Rules (Copilot Rule #12):
    /// - Endpoints MUST be defined in Minimal API style only
    /// - Every endpoint MUST be documented with OpenAPI attributes
    /// - Every response MUST be typed — no anonymous objects
    /// - Business logic MUST NOT live in endpoints — delegate to MediatR
    ///
    /// Usage — implement in each module:
    ///   public sealed class ProjectsModuleEndpoints : IModuleEndpoints
    ///   {
    ///       public void MapEndpoints(IEndpointRouteBuilder app)
    ///       {
    ///           var group = app.MapGroup("/api/v1/projects")
    ///               .WithTags("Projects")
    ///               .RequireAuthorization();
    ///
    ///           group.MapPost("/", CreateProjectEndpoint.Handle)
    ///               .WithName("CreateProject")
    ///               .WithSummary("Create a new project")
    ///               .Produces{ProjectCreatedResponse}(201)
    ///               .ProducesValidationProblem();
    ///
    ///           group.MapGet("/{id:guid}", GetProjectEndpoint.Handle)
    ///               .WithName("GetProject")
    ///               .WithSummary("Get project by ID")
    ///               .Produces{ProjectDetailResponse}(200)
    ///               .Produces(404);
    ///       }
    ///   }
    ///
    /// Registration at startup (Program.cs):
    ///   app.MapModuleEndpoints(); // discovers all IModuleEndpoints implementations
    /// </summary>
    public interface IModuleEndpoints
    {
        /// <summary>
        /// Registers all endpoints for this module.
        /// Called once at application startup during endpoint registration.
        /// </summary>
        /// <param name="app">The endpoint route builder to register endpoints on.</param>
        void MapEndpoints(IEndpointRouteBuilder app);
    }
}
