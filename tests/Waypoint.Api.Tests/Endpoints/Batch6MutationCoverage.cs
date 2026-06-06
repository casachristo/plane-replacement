using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Waypoint.Api.Middleware;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class ProjectEndpointsExtra3MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectEndpointsExtra3MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task GET_list_after_no_creates_returns_empty_array_not_null()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects");
        var list = await resp.Content.ReadFromJsonAsync<List<ProjectDto>>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_returns_DTO_with_Slug_NOT_uppercased()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("lowercase-slug", "Name", "LCS"))).Content.ReadFromJsonAsync<ProjectDto>();
        dto!.Slug.Should().Be("lowercase-slug");   // slug preserved as-is, not uppercased
    }

    [Fact]
    public async Task POST_returns_DTO_with_Identifier_as_given()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("pid", "Name", "PIDX"))).Content.ReadFromJsonAsync<ProjectDto>();
        dto!.Identifier.Should().Be("PIDX");
    }
}

public class ErrorEnvelopeExtraMutationCoverage
{
    private static async Task<(int status, ErrorResponse? body)> InvokeWithThrow(Exception toThrow, int? requestStatusBeforeThrow = null)
    {
        var middleware = new ErrorEnvelopeMiddleware(c =>
        {
            if (requestStatusBeforeThrow is not null) c.Response.StatusCode = requestStatusBeforeThrow.Value;
            throw toThrow;
        }, NullLogger<ErrorEnvelopeMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        var envelope = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return (ctx.Response.StatusCode, envelope);
    }

    [Fact]
    public async Task WaypointException_envelope_uses_exception_StatusCode_not_some_default()
    {
        // Custom WaypointException with 418.
        var (status, _) = await InvokeWithThrow(new WaypointException("teapot", "I'm a teapot", 418));
        status.Should().Be(418);
    }

    [Fact]
    public async Task Envelope_contains_RequestId_field_string()
    {
        var (_, body) = await InvokeWithThrow(new NotFoundException("x", "y"));
        body!.RequestId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Envelope_status_code_for_default_WaypointException_is_set_correctly()
    {
        // WaypointException base ctor takes explicit status; pin 500 here.
        var (status, _) = await InvokeWithThrow(new WaypointException("x", "y", 500));
        status.Should().Be(500);
    }
}

public class IdempotencyEvictionMutationCoverage
{
    [Fact]
    public async Task Two_distinct_keys_each_cache_independently()
    {
        var keyA = $"k-{Guid.NewGuid()}";
        var keyB = $"k-{Guid.NewGuid()}";

        var middleware = new IdempotencyMiddleware(async c =>
        {
            c.Response.StatusCode = 200;
            c.Response.ContentType = "application/json";
            await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("body-A"));
        });
        var ctxA = new DefaultHttpContext();
        ctxA.Request.Method = "POST";
        ctxA.Request.Headers[IdempotencyMiddleware.HeaderName] = keyA;
        ctxA.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctxA);

        var nextCalledB = false;
        var middlewareB = new IdempotencyMiddleware(async c =>
        {
            nextCalledB = true;
            c.Response.StatusCode = 201;
            c.Response.ContentType = "application/json";
            await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("body-B"));
        });
        var ctxB = new DefaultHttpContext();
        ctxB.Request.Method = "POST";
        ctxB.Request.Headers[IdempotencyMiddleware.HeaderName] = keyB;
        ctxB.Response.Body = new MemoryStream();
        await middlewareB.InvokeAsync(ctxB);

        // keyB must invoke next() because it's a different key (not cached).
        nextCalledB.Should().BeTrue();
        ctxB.Response.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task PUT_method_does_not_engage_cache()
    {
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(c => { nextCalled = true; return Task.CompletedTask; });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "PUT";   // not POST or PATCH
        ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = "any";
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        nextCalled.Should().BeTrue();
    }
}
