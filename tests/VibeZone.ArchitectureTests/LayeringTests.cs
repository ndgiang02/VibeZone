using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using VibeZone.Infrastructure.Modules;
using VibeZone.Migrations;
using VibeZone.SharedKernel;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace VibeZone.ArchitectureTests;

/// <summary>Enforces the dependency rules in AGENTS.md §4 / ADR-0001.</summary>
public sealed class LayeringTests
{
    private const string Frameworks = @"^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Npgsql|Amazon|Serilog|OpenTelemetry)(\.|$)";

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(DomainError).Assembly,
            typeof(IModule).Assembly,
            typeof(AppModules).Assembly,
            typeof(Program).Assembly)
        .LoadAssemblies(AppModules.Create().Modules.Select(m => m.Assembly).Distinct().ToArray())
        .Build();

    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in AppModules.Create().Modules)
        {
            data.Add(module.GetType().Namespace!.Split('.')[^1]);
        }

        return data;
    }

    [Fact]
    public void SharedKernel_has_no_framework_or_project_dependencies() =>
        AssertNoViolations(Types().That().ResideInNamespaceMatching(@"^VibeZone\.SharedKernel(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching($@"{Frameworks}|^VibeZone\.(?!SharedKernel)"));

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_depends_only_on_SharedKernel(string module) =>
        AssertNoViolations(Types().That().ResideInNamespaceMatching($@"^VibeZone\.Modules\.{module}\.Domain(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                $@"{Frameworks}|^VibeZone\.Infrastructure|^VibeZone\.Modules\.{module}\.(Application|Infrastructure|Endpoints)"));

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_does_not_depend_on_infrastructure_or_web(string module) =>
        AssertNoViolations(Types().That().ResideInNamespaceMatching($@"^VibeZone\.Modules\.{module}\.Application(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                $@"{Frameworks}|^VibeZone\.Infrastructure|^VibeZone\.Modules\.{module}\.(Infrastructure|Endpoints)"));

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_does_not_reach_into_other_modules(string module) =>
        AssertNoViolations(Types().That().ResideInNamespaceMatching($@"^VibeZone\.Modules\.{module}(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching($@"^VibeZone\.Modules\.(?!{module}(\.|$))(?!\w+\.Contracts(\.|$))"));

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_infrastructure_is_not_public(string module) =>
        AssertNoViolations(Types().That().ResideInNamespaceMatching($@"^VibeZone\.Modules\.{module}\.Infrastructure(\.|$)")
            .Should().NotBePublic());

    [Fact]
    public void Every_module_is_discovered()
    {
        // Guards against the theories above silently passing with no modules.
        Assert.NotEmpty(ModuleNames());
        Assert.NotEmpty(Types().That().ResideInNamespaceMatching(@"^VibeZone\.Modules\.Catalog\.Domain").GetObjects(Architecture));
    }

    private static void AssertNoViolations(IArchRule rule)
    {
        var violations = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();
        Assert.True(violations.Count == 0, $"{rule.Description}{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }
}
