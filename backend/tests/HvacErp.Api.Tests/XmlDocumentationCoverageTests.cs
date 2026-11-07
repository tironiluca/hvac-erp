using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace HvacErp.Api.Tests;

/// <summary>
/// Enforces the project's documentation rule: every public type and every public method declared in
/// the four src/ assemblies has a <c>&lt;summary&gt;</c> (or <c>&lt;inheritdoc/&gt;</c>) in its XML doc file.
/// Interface implementations and overrides are covered by the documented contract they implement.
/// EF migrations (generated code) are exempt.
/// </summary>
public class XmlDocumentationCoverageTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(Domain.Entities.Quote).Assembly,
        typeof(Application.DependencyInjection).Assembly,
        typeof(Infrastructure.DependencyInjection).Assembly,
        typeof(Authorization.Policies).Assembly,
    ];

    [Fact]
    public void Every_public_type_and_method_is_documented()
    {
        var missing = Assemblies.SelectMany(FindUndocumented).OrderBy(x => x).ToList();

        Assert.True(missing.Count == 0, $"{missing.Count} undocumented public members:\n" + string.Join("\n", missing));
    }

    private static IEnumerable<string> FindUndocumented(Assembly assembly)
    {
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
        Assert.True(File.Exists(xmlPath), $"Missing XML doc file {xmlPath} — is GenerateDocumentationFile on?");

        var documented = XDocument.Load(xmlPath).Descendants("member")
            .Where(m => m.Element("summary") is not null || m.Element("inheritdoc") is not null)
            .Select(m => (string)m.Attribute("name")!)
            .ToHashSet();

        bool HasDoc(string prefix) => documented.Contains(prefix) || documented.Any(d => d.StartsWith(prefix + "(", StringComparison.Ordinal));

        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) == true
                || type.IsDefined(typeof(CompilerGeneratedAttribute)))
            {
                continue;
            }

            var typeId = DocId(type);
            if (!documented.Contains("T:" + typeId))
            {
                yield return "T:" + typeId;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName
                    || method.IsDefined(typeof(CompilerGeneratedAttribute))
                    || IsRecordSynthesized(type, method)
                    || method.GetBaseDefinition() != method
                    || ImplementsInterface(type, method))
                {
                    continue;
                }

                var id = $"M:{typeId}.{method.Name}";
                if (!HasDoc(id))
                {
                    yield return id;
                }
            }
        }
    }

    private static string DocId(Type type) => (type.FullName ?? type.Name).Replace('+', '.').Split('`')[0];

    private static bool IsRecordSynthesized(Type type, MethodInfo method) =>
        type.GetMethod("<Clone>$") is not null
        && method.Name is "ToString" or "Equals" or "GetHashCode" or "Deconstruct" or "PrintMembers" or "op_Equality" or "op_Inequality";

    private static bool ImplementsInterface(Type type, MethodInfo method) =>
        !type.IsInterface && type.GetInterfaces()
            .Select(type.GetInterfaceMap)
            .Any(map => map.TargetMethods.Contains(method));
}
