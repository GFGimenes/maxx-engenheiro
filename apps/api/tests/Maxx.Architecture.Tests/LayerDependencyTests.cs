using System.Reflection;

namespace Maxx.Architecture.Tests;

// Api conhece Application. Application conhece Domain. Infrastructure implementa o que
// Application e Domain declaram. Domain não conhece ninguém. A Api também referencia a
// Infrastructure, mas só como raiz de composição (registro de dependências).
public class LayerDependencyTests
{
    [Fact]
    public void Domain_nao_depende_de_nenhum_projeto_Maxx()
    {
        Assert.Empty(MaxxReferencesOf("Maxx.Domain"));
    }

    [Fact]
    public void Domain_so_referencia_a_biblioteca_padrao()
    {
        var references = Assembly.Load(new AssemblyName("Maxx.Domain"))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard",
            $"Maxx.Domain referencia {name}."));
    }

    [Fact]
    public void Application_depende_so_do_Domain()
    {
        Assert.Subset(new HashSet<string> { "Maxx.Domain" }, MaxxReferencesOf("Maxx.Application"));
    }

    [Fact]
    public void Infrastructure_nao_depende_da_Api()
    {
        Assert.Subset(
            new HashSet<string> { "Maxx.Domain", "Maxx.Application" },
            MaxxReferencesOf("Maxx.Infrastructure"));
    }

    private static HashSet<string> MaxxReferencesOf(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Maxx.", StringComparison.Ordinal))
            .ToHashSet();
}
