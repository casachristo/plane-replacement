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
}
