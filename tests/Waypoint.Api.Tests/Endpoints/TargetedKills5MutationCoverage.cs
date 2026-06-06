using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Waypoint.Api.Middleware;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills5MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills5MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task ErrorEnvelope_ConflictException_envelope_carries_specific_code()
    {
        var middleware = new ErrorEnvelopeMiddleware(_ => throw new ConflictException("duplicate", "Already exists."),
            NullLogger<ErrorEnvelopeMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        var env = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        env!.Error.Code.Should().Be("duplicate");
    }

    [Fact]
    public async Task ErrorEnvelope_ValidationException_envelope_carries_specific_message()
    {
        var middleware = new ErrorEnvelopeMiddleware(_ => throw new ValidationException("bad_thing", "the message"),
            NullLogger<ErrorEnvelopeMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        var env = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        env!.Error.Message.Should().Be("the message");
    }

    [Fact]
    public async Task Idempotency_PUT_with_key_passes_through()
    {
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "PUT";
        ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = $"k-{Guid.NewGuid()}";
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RequestId_assigned_value_is_a_valid_GUID_when_no_header()
    {
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx);
        var rid = ctx.Items["RequestId"]?.ToString();
        rid.Should().NotBeNullOrEmpty();
        Guid.TryParse(rid, out _).Should().BeTrue();
    }

    [Fact]
    public async Task SurfaceGuard_path_starting_with_api_v2_passes_through()
    {
        var middleware = new Waypoint.Api.Auth.SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v2/projects";   // /api/v2/ doesn't match /api/v1/ prefix
        ctx.Request.Headers.Authorization = "Bearer wpt_test_secret";
        ctx.Response.Body = new MemoryStream();
        var nextCalled = false;
        var mw2 = new Waypoint.Api.Auth.SurfaceGuardMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        await mw2.InvokeAsync(ctx);
        nextCalled.Should().BeTrue();
    }
}
