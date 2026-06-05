using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Waypoint.Api.Middleware;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

/// <summary>
/// Mutation-coverage tests for ErrorEnvelopeMiddleware. Drives the middleware
/// with simulated downstream throws and asserts the response envelope shape +
/// status code each WaypointException subtype maps to.
/// </summary>
public class ErrorEnvelopeMiddlewareTestsMutationCoverage
{
    private static async Task<(int status, ErrorResponse? body)> InvokeWithThrow(Exception toThrow)
    {
        var middleware = new ErrorEnvelopeMiddleware(_ => throw toThrow,
            NullLogger<ErrorEnvelopeMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        var body = new MemoryStream();
        ctx.Response.Body = body;
        await middleware.InvokeAsync(ctx);
        body.Position = 0;
        var envelope = await JsonSerializer.DeserializeAsync<ErrorResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return (ctx.Response.StatusCode, envelope);
    }

    [Fact]
    public async Task NotFoundException_maps_to_404_with_envelope_code()
    {
        var (status, body) = await InvokeWithThrow(new NotFoundException("issue_not_found", "nope"));
        status.Should().Be(404);
        body!.Error.Code.Should().Be("issue_not_found");
        body.Error.Message.Should().Be("nope");
    }

    [Fact]
    public async Task ConflictException_maps_to_409()
    {
        var (status, _) = await InvokeWithThrow(new ConflictException("slug_exists", "dup"));
        status.Should().Be(409);
    }

    [Fact]
    public async Task ValidationException_maps_to_422()
    {
        var (status, body) = await InvokeWithThrow(new ValidationException("bad_input", "fix it"));
        status.Should().Be(422);
        body!.Error.Code.Should().Be("bad_input");
    }

    [Fact]
    public async Task ValidationException_details_are_in_envelope()
    {
        var details = new Dictionary<string, object> { ["required"] = "admin" };
        var (_, body) = await InvokeWithThrow(new ValidationException("missing_scope", "need scope", details));
        body!.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public async Task PreconditionFailedException_maps_to_412()
    {
        var (status, body) = await InvokeWithThrow(new PreconditionFailedException(
            "acceptance_criteria_unchecked", "gate fired"));
        status.Should().Be(412);
        body!.Error.Code.Should().Be("acceptance_criteria_unchecked");
    }

    [Fact]
    public async Task Generic_exception_maps_to_500_internal_error()
    {
        // Kills: "internal_error" → "" + 500 → 0 mutations.
        var (status, body) = await InvokeWithThrow(new InvalidOperationException("oops"));
        status.Should().Be(500);
        body!.Error.Code.Should().Be("internal_error");
    }

    [Fact]
    public async Task Generic_exception_does_NOT_leak_inner_message_to_envelope()
    {
        var (_, body) = await InvokeWithThrow(new InvalidOperationException("secret-detail-not-leaked"));
        body!.Error.Message.Should().NotContain("secret-detail-not-leaked");
    }

    [Fact]
    public async Task No_exception_does_not_overwrite_response()
    {
        // Baseline: middleware must not touch status when next() returns cleanly.
        var middleware = new ErrorEnvelopeMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, NullLogger<ErrorEnvelopeMiddleware>.Instance);
        var c = new DefaultHttpContext();
        c.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(c);
        c.Response.StatusCode.Should().Be(200);
    }
}
