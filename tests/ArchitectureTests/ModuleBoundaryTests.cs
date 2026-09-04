using System.Reflection;
using NetArchTest.Rules;

namespace UMS.ArchitectureTests;

/// <summary>
/// Build-time enforcement of ADR-0002 ("a build-time architecture test ... fails CI if a module
/// references another module's internals directly"). No module exists yet
/// (release/DEVELOPMENT_PLAN.md - Identity is Flow #4), so <see cref="Modules_do_not_reference_another_modules_internals"/>
/// runs against zero discovered module assemblies today and passes vacuously - it activates
/// automatically, with no test-project change required, the moment the first
/// <c>UMS.Modules.*.dll</c> lands next to this test assembly.
/// </summary>
public class ModuleBoundaryTests
{
    [Fact]
    public void Shared_libraries_do_not_depend_on_host_or_workers()
    {
        var sharedAssemblies = new[]
        {
            typeof(UMS.Shared.Observability.DependencyInjection).Assembly,
            typeof(UMS.Shared.ErrorHandling.DependencyInjection).Assembly,
            typeof(UMS.Shared.Resilience.DependencyInjection).Assembly,
        };

        foreach (var assembly in sharedAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny("UMS.Host", "UMS.Workers")
                .GetResult();

            Assert.True(result.IsSuccessful, FormatFailure(assembly, "UMS.Host/UMS.Workers", result));
        }
    }

    [Fact]
    public void Modules_do_not_reference_another_modules_internals()
    {
        var moduleAssemblies = LoadModuleAssemblies();

        foreach (var assembly in moduleAssemblies)
        {
            var moduleName = ExtractModuleName(assembly.GetName().Name!);

            // Excludes every assembly belonging to THIS SAME module (not just this exact
            // assembly) - a module's own Api is expected and required to depend on its own
            // Application/Domain (that dependency is the whole point of an Api layer); the rule
            // this test enforces is "no OTHER module reaches in", never "no layer within the same
            // module may depend on another layer of itself".
            foreach (var other in moduleAssemblies.Where(a => ExtractModuleName(a.GetName().Name!) != moduleName))
            {
                var result = Types.InAssembly(other)
                    .Should()
                    .NotHaveDependencyOnAny(
                        $"UMS.Modules.{moduleName}.Domain",
                        $"UMS.Modules.{moduleName}.Application",
                        $"UMS.Modules.{moduleName}.Infrastructure")
                    .GetResult();

                Assert.True(result.IsSuccessful, FormatFailure(other, moduleName, result));
            }
        }
    }

    private static string FormatFailure(Assembly offendingAssembly, string forbiddenTarget, TestResult result) =>
        $"{offendingAssembly.GetName().Name} illegally depends on {forbiddenTarget}: " +
        string.Join(", ", result.FailingTypeNames ?? []);

    private static string ExtractModuleName(string assemblyName)
    {
        var parts = assemblyName.Split('.');
        return parts.Length >= 3 ? parts[2] : assemblyName;
    }

    private static Assembly[] LoadModuleAssemblies() =>
        Directory.Exists(AppContext.BaseDirectory)
            ? Directory.GetFiles(AppContext.BaseDirectory, "UMS.Modules.*.dll")
                .Select(Assembly.LoadFrom)
                .ToArray()
            : [];
}
