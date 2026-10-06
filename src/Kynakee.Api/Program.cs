using Kynakee.Api.Endpoints;

namespace Kynakee.Api
{
    internal sealed class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add Dependence Container extensions
            builder.Services.AddApiDependencies(builder.Configuration);

            // Add services to the container.
            builder.Services.AddHealthChecks();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseRateLimiter();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHealthChecks("/health");
            app.MapIdentityEndpoints();
            app.MapBillingEndpoints();
            app.MapFallbackToFile("/modelo-datos/{*path:nonfile}", "modelo-datos/index.html");
                                      

            app.Run();
        }
    }
}
