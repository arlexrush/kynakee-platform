namespace Kynakee.Modules.Identity.Domain.ValueObjects;

public enum TenantType
{
    Individual,
    SME,
    Company,
    Promoter
}

public enum TenantStatus
{
    Active,
    Suspended,
    Cancelled
}

public enum UserStatus
{
    Active,
    Inactive,
    PendingVerification
}

public enum UserRole
{
    Owner,
    Admin,
    Technician,
    Commercial,
    Viewer
}