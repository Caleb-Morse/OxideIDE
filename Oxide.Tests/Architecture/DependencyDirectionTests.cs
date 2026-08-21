using System.Reflection;
using Oxide.Core;
using Oxide.Syntax;

namespace Oxide.Tests.Architecture;

public sealed class DependencyDirectionTests
{
    [Theory]
    [MemberData(nameof(NonPresentationAssemblies))]
    public void NonPresentationAssemblies_do_not_reference_Avalonia(Assembly assembly)
    {
        var avaloniaReferences = assembly
            .GetReferencedAssemblies()
            .Where(reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) is true)
            .Select(reference => reference.FullName)
            .ToArray();

        Assert.Empty(avaloniaReferences);
    }

    public static TheoryData<Assembly> NonPresentationAssemblies => new()
    {
        typeof(ApplicationInfo).Assembly,
        SyntaxAssembly.Marker.Assembly,
    };
}
