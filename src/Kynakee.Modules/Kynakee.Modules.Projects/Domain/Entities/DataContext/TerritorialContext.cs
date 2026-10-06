namespace Kynakee.Modules.Projects.Domain.Entities.DataContext
{
    public sealed record TerritorialContext(
    string? Country,
    string? Region,
    string? Province,
    string? Municipality,
    string? Address);
}
