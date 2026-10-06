namespace Kynakee.Modules.Projects.Domain.Entities.DataContext
{
    public sealed record LaborContext(
    string? CollectiveAgreement,
    decimal? SalaryOfficial1,
    decimal? SalaryLaborer);
}
