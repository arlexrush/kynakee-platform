using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Infrastructure;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Infrastructure.Email;
using Kynakee.Modules.Identity.Contracts;

namespace Kynakee.Modules.Identity
{
    public static class IdentityModuleDependencyExtensions
    {
        public static IServiceCollection AddIdentityModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<IdentityDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"));
            });

            services.AddScoped<IModuleTransactionParticipant,
                IdentityTransactionParticipant>();

            services.AddScoped<IIdentityRepository, IdentityRepository>();
            services.AddScoped<ITenantManagementAuthorization, TenantManagementAuthorization>();
            services.AddIdentityCore<User>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 12;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireDigit = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddUserStore<IdentityUserStore>();
            services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            services.TryAddSingleton<JwtIdentityTokenService>();
            services.TryAddSingleton<IIdentityTokenService>(provider =>
                provider.GetRequiredService<JwtIdentityTokenService>());
            services.TryAddSingleton<ISmtpInvitationTransport, SmtpInvitationTransport>();
            services.TryAddScoped<IInvitationEmailSender, SmtpInvitationEmailSender>();

            services.AddValidatorsFromAssemblyContaining<RegisterTenantOwnerCommandValidator>();

            return services;
        }   
    }
}
