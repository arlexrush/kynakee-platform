using Kynakee.Api.Infrastructure.ExceptionHandling;
using Kynakee.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public sealed class IdentityUniqueConstraintExceptionHandlerTests
{
    [Theory]
    [InlineData("ux_identity_users_normalized_email", "IDENTITY_EMAIL_ALREADY_REGISTERED")]
    [InlineData("ux_identity_tenants_slug", "IDENTITY_TENANT_SLUG_ALREADY_USED")]
    [InlineData("ux_identity_tenant_users_user", "IDENTITY_TENANT_USER_ALREADY_EXISTS")]
    [InlineData("ux_identity_refresh_tokens_hash", "IDENTITY_REFRESH_TOKEN_HASH_CONFLICT")]
    [InlineData("ux_identity_invitations_hash", "IDENTITY_INVITATION_TOKEN_HASH_CONFLICT")]
    public async Task AllowedIdentityConstraintShouldReturnStableErrorCode(string constraint, string code)
    {
        var context = CreateContext("/api/v1/identity/auth/register");
        var handler = new IdentityUniqueConstraintExceptionHandler();

        await handler.TryHandleAsync(context,
            new DbUpdateException("Commit failed.", CreatePostgresException(PostgresErrorCodes.UniqueViolation, "schema_identity", constraint)),
            CancellationToken.None);

        Assert.Contains(code, await ReadBodyAsync(context), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdentityConstraintInOtherSchemaShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/identity/auth/register");
        var handler = new IdentityUniqueConstraintExceptionHandler();

        var handled = await handler.TryHandleAsync(context,
            CreatePostgresException(PostgresErrorCodes.UniqueViolation, "schema_other", "ux_identity_users_normalized_email"),
            CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task AllowedIdentityUniqueViolationShouldReturnConflict()
    {
        var context = CreateContext("/api/v1/identity/auth/register");
        var handler = new IdentityUniqueConstraintExceptionHandler();
        var exception = CreatePostgresException(PostgresErrorCodes.UniqueViolation, "schema_identity", "ux_identity_users_normalized_email");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.DoesNotContain("duplicate key", await ReadBodyAsync(context), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonIdentityConstraintShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/identity/auth/register");
        var handler = new IdentityUniqueConstraintExceptionHandler();
        var exception = CreatePostgresException(PostgresErrorCodes.UniqueViolation, "schema_identity", "ux_unknown_constraint");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task ConcurrencyExceptionOnIfMatchWriteShouldReturnPreconditionFailed()
    {
        var context = CreateContext("/api/v1/identity/tenants/me");
        context.Request.Method = HttpMethods.Put;
        context.Request.Headers.IfMatch = new IdentityEntityTagProtector(new string('k', 32))
            .Create(Guid.NewGuid(), IdentityEntityTagResource.User, Guid.NewGuid(), 1);
        var handler = new IdentityUniqueConstraintExceptionHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new DbUpdateConcurrencyException("The resource has changed."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, context.Response.StatusCode);
    }

    [Fact]
    public async Task ConcurrencyExceptionWithoutIfMatchShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/identity/tenants/me");
        context.Request.Method = HttpMethods.Put;
        var handler = new IdentityUniqueConstraintExceptionHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new DbUpdateConcurrencyException("The resource has changed."),
            CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task ConcurrencyExceptionOnUnrelatedIdentityWriteShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/identity/auth/login");
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers.IfMatch = new IdentityEntityTagProtector(new string('k', 32))
            .Create(Guid.NewGuid(), IdentityEntityTagResource.User, Guid.NewGuid(), 1);
        var handler = new IdentityUniqueConstraintExceptionHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new DbUpdateConcurrencyException("The resource has changed."),
            CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task NonUniqueViolationShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/identity/auth/register");
        var handler = new IdentityUniqueConstraintExceptionHandler();
        var exception = CreatePostgresException(PostgresErrorCodes.ForeignKeyViolation, "schema_identity", "ux_identity_users_normalized_email");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task NonIdentityRouteShouldNotBeHandled()
    {
        var context = CreateContext("/api/v1/billing/credits");
        var handler = new IdentityUniqueConstraintExceptionHandler();
        var exception = CreatePostgresException(PostgresErrorCodes.UniqueViolation, "schema_identity", "ux_identity_users_normalized_email");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        return context;
    }

    private static PostgresException CreatePostgresException(string sqlState, string schemaName, string constraintName) =>
        new("database error", "ERROR", "ERROR", sqlState, schemaName: schemaName, constraintName: constraintName);

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
