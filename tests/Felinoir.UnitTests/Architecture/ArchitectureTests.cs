using System.Reflection;
using Felinoir.Application.Common.Interfaces;
using FluentValidation;
using Mediator;
using NetArchTest.Rules;

namespace Felinoir.UnitTests.Architecture;

/// <summary>
/// The dependency rule and the conventions of ADR 0002, checked on every build: dependencies
/// point inward, endpoints stay thin, queries skip the write side, and every command is validated.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Felinoir.Domain.Entities.Movie).Assembly;
    private static readonly Assembly Application = typeof(Felinoir.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Felinoir.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_else_in_the_solution_and_no_framework() =>
        AssertNoDependency(Types.InAssembly(Domain),
            "Felinoir.Application", "Felinoir.Infrastructure", "Felinoir.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Mediator", "FluentValidation", "Npgsql");

    [Fact]
    public void Application_depends_on_neither_infrastructure_nor_the_web() =>
        AssertNoDependency(Types.InAssembly(Application),
            "Felinoir.Infrastructure", "Felinoir.Api", "Microsoft.AspNetCore", "Npgsql");

    [Fact]
    public void Infrastructure_does_not_depend_on_the_web_host() =>
        AssertNoDependency(Types.InAssembly(Infrastructure), "Felinoir.Api", "Microsoft.AspNetCore");

    [Fact]
    public void Endpoints_reach_data_only_through_the_application() =>
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace("Felinoir.Api.Endpoints"),
            "Microsoft.EntityFrameworkCore", "Felinoir.Infrastructure");

    [Fact]
    public void Query_handlers_skip_repositories_and_the_unit_of_work()
    {
        var offenders = Implementing(typeof(IQueryHandler<,>))
            .Where(handler => Dependencies(handler).Any(t =>
                t == typeof(IUnitOfWork) || (t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))))
            .Select(t => t.FullName);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_command_has_a_validator()
    {
        var validated = Application.GetTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        var unvalidated = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseCommand).IsAssignableFrom(t))
            .Where(t => !validated.Contains(t))
            .Select(t => t.FullName);

        Assert.Empty(unvalidated);
    }

    [Fact]
    public void Handlers_are_sealed()
    {
        var unsealed = Implementing(typeof(IQueryHandler<,>), typeof(ICommandHandler<,>), typeof(ICommandHandler<>))
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName);

        Assert.Empty(unsealed);
    }

    private static void AssertNoDependency(PredicateList types, params string[] forbidden) =>
        AssertNoDependency(types.Should(), forbidden);

    private static void AssertNoDependency(Types types, params string[] forbidden) =>
        AssertNoDependency(types.Should(), forbidden);

    private static void AssertNoDependency(Conditions should, string[] forbidden)
    {
        var result = should.NotHaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful,
            "Forbidden dependencies in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    private static IEnumerable<Type> Implementing(params Type[] genericInterfaces) =>
        Application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i =>
            i.IsGenericType && genericInterfaces.Contains(i.GetGenericTypeDefinition())));

    /// <summary>The types a class takes in its constructors.</summary>
    private static IEnumerable<Type> Dependencies(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
}
