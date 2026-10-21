using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog.AspNetCore;
using Serilog;

namespace Kynakee.Gateway
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext());

            builder.Services.AddOptions<GatewayAuthenticationOptions>()
                .Bind(builder.Configuration.GetSection("Authentication"))
                .Validate(options =>
                        !string.IsNullOrWhiteSpace(options.SigningKey) &&
                        System.Text.Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
                    "Authentication:SigningKey must be configured with at least 32 UTF-8 bytes.")
                .ValidateOnStart();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            /// Configure JWT Bearer options using the bound GatewayAuthenticationOptions
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<GatewayAuthenticationOptions>>((options, authenticationOptions) =>
                {
                    var authentication = authenticationOptions.Value;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authentication.Issuer,
                        ValidateAudience = true,
                        ValidAudience = authentication.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            System.Text.Encoding.UTF8.GetBytes(authentication.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                })
                .ValidateOnStart();


            builder.Services.AddAuthorization();


            builder.Services.AddSingleton<RedisConnectionFactory>(); // Register RedisConnectionFactory as a singleton
            builder.Services.AddOptions<RedisRateLimitOptions>()
                .Bind(builder.Configuration.GetSection("RateLimiting"))
                .Validate(options => options.PermitLimit > 0 && options.WindowSeconds > 0,
                    "Rate limiting values must be greater than zero.")
                .ValidateOnStart();


            builder.Services.AddHealthChecks()
                .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
                .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Middleware configuration
            app.UseMiddleware<CorrelationIdMiddleware>(); // Serve the CorrelationIdMiddleware to generate and propagate correlation IDs
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant_id")?.Value ?? "anonymous");
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? "anonymous");
                    diagnosticContext.Set("CorrelationId", httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
                };
            }); // Use Serilog request logging middleware with enriched diagnostic context for tenant ID, user ID, and correlation ID


            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi(); // Map OpenAPI endpoints for development environment
            }

            // Use authentication and authorization middleware
            app.UseAuthentication();
            app.UseMiddleware<RedisSlidingWindowRateLimiterMiddleware>(); // Use custom rate limiting middleware
            app.UseAuthorization();

            app.MapHealthChecks("/health");
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });
            app.MapReverseProxy(); // Map reverse proxy endpoints   

            app.Run();
        }
    }
}
