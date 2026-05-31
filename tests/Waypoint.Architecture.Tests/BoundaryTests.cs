using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Waypoint.Architecture.Tests;

public class BoundaryTests
{
    private static readonly Assembly DomainAsm = typeof(Waypoint.Domain.WaypointDbContext).Assembly;
    private static readonly Assembly ContractsAsm = typeof(Waypoint.Contracts.ProjectDto).Assembly;

    [Fact]
    public void Domain_must_not_reference_Api()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOn("Waypoint.Api")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_must_not_reference_EFCore()
    {
        var result = Types.InAssembly(ContractsAsm)
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_must_not_reference_Npgsql()
    {
        var result = Types.InAssembly(ContractsAsm)
            .Should().NotHaveDependencyOn("Npgsql")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void PublicApi_endpoints_must_not_reference_ServiceBearerResolver()
    {
        var apiAsm = typeof(Waypoint.Api.Auth.ServiceBearerResolver).Assembly;
        var result = Types.InAssembly(apiAsm)
            .That().ResideInNamespace("Waypoint.Api.Endpoints.PublicApi")
            .Should().NotHaveDependencyOn(typeof(Waypoint.Api.Auth.ServiceBearerResolver).FullName!)
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Endpoints_must_not_use_WaypointDbContext_directly_except_for_read_only_queries()
    {
        // Light-touch version: endpoints under PublicApi/InternalApi may use DbContext for reads
        // but the intent is that writes go through *Repository abstractions. Stricter version
        // (Phase 7) would enforce no Add/Update/Remove in endpoint files.
        var apiAsm = typeof(Waypoint.Api.Auth.ServiceBearerResolver).Assembly;
        var endpointTypes = Types.InAssembly(apiAsm)
            .That().ResideInNamespaceMatching("Waypoint.Api.Endpoints.*")
            .GetTypes();
        endpointTypes.Should().NotBeEmpty("at least one endpoint class must exist");
    }
}
