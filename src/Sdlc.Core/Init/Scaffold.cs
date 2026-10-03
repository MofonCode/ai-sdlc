using System.Globalization;
using System.Reflection;
using System.Text;

namespace Sdlc.Core.Init;

/// <summary>
/// The solution a scaffolded repository starts as.
/// </summary>
/// <remarks>
/// <para>A solution file at the root, one source project, a unit test project, and an architecture test
/// project carrying the fitness functions — plus one reference slice written in the operation shape.</para>
///
/// <para><strong>The slice is the point.</strong> An agent takes the shape of the code beside it far
/// more reliably than it follows an instruction, so the single most effective thing a profile can ship
/// is one worked example of the shape it wants. The evidence is direct: in the previous implementation
/// of a solution governed this way, a second bounded context copied the first file-for-file — the
/// nesting, the registration, the try/catch, even a hardcoded literal — because the first was simply
/// there.</para>
///
/// <para><strong>The architecture tests cannot ship alone.</strong> Their own guard throws when a scan
/// finds nothing, so a project to police has to exist in the same breath. That is why the slice and the
/// rules are one scaffold rather than two features.</para>
///
/// <para>Templates are embedded resources rather than string constants. The gate publishes single-file,
/// so a template read from a path beside the binary is missing in exactly that case; a resource is
/// inside the assembly. They carry a <c>.template</c> extension so nothing reads as source that is
/// never compiled, and <c>ScaffoldTests</c> builds the generated result, which is what keeps them from
/// rotting unnoticed.</para>
/// </remarks>
public static class Scaffold
{
    /// <summary>The token every template carries where the solution's name belongs.</summary>
    private const string Token = "__SOLUTION__";

    /// <summary>
    /// Turns a directory name into something that can be a namespace and an assembly name.
    /// </summary>
    /// <remarks>
    /// A repository directory is often <c>dice-trial</c> or <c>my_app</c>, and neither is a legal C#
    /// identifier. Separators become word boundaries, anything else illegal is dropped, and a name that
    /// survives as nothing or as a leading digit falls back rather than producing a solution that
    /// cannot compile.
    /// </remarks>
    /// <param name="directoryName">The repository's directory name.</param>
    /// <returns>A PascalCase identifier.</returns>
    public static string NameFrom(string directoryName)
    {
        ArgumentNullException.ThrowIfNull(directoryName);

        StringBuilder name = new();
        bool capitalise = true;

        foreach (char character in directoryName)
        {
            if (character is '-' or '_' or ' ' or '.')
            {
                capitalise = true;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            name.Append(capitalise ? char.ToUpper(character, CultureInfo.InvariantCulture) : character);
            capitalise = false;
        }

        string result = name.ToString();

        return result.Length == 0 || char.IsDigit(result[0]) ? "Solution" : result;
    }

    /// <summary>
    /// Every file the scaffold writes, keyed by its path relative to the solution root.
    /// </summary>
    /// <param name="name">The solution's name, from <see cref="NameFrom"/>.</param>
    /// <returns>The paths and their contents, in writing order.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Files(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string core = Path.Combine("src", name + ".Core");
        string api = Path.Combine("src", name + ".Api");
        string tests = Path.Combine("tests", name + ".Tests");
        string architecture = Path.Combine("tests", name + ".Architecture.Tests");

        return
        [
            Entry(name + ".slnx", "Solution.slnx", name),
            Entry("BannedSymbols.txt", "BannedSymbols.txt", name),

            Entry(Path.Combine(core, name + ".Core.csproj"), "Core.csproj", name),
            Entry(Path.Combine(core, name + "Services.cs"), "Services.cs", name),
            Entry(Path.Combine(core, "ValidationBehavior.cs"), "ValidationBehavior.cs", name),
            Entry(Path.Combine(core, "Greetings", "Greet.cs"), "Greet.cs", name),

            Entry(Path.Combine(api, name + ".Api.csproj"), "Api.csproj", name),
            Entry(Path.Combine(api, "Program.cs"), "Program.cs", name),
            Entry(Path.Combine(api, "Boundary.cs"), "Boundary.cs", name),
            Entry(Path.Combine(api, "Greetings", "GreetEndpoints.cs"), "GreetEndpoints.cs", name),

            Entry(Path.Combine(tests, name + ".Tests.csproj"), "Tests.csproj", name),
            Entry(Path.Combine(tests, "Gate.cs"), "Gate.cs", name),
            Entry(Path.Combine(tests, "GreetTests.cs"), "GreetTests.cs", name),
            Entry(Path.Combine(tests, "BoundaryTests.cs"), "BoundaryTests.cs", name),

            Entry(
                Path.Combine(architecture, name + ".Architecture.Tests.csproj"),
                "ArchitectureTests.csproj",
                name),
            Entry(Path.Combine(architecture, "RepoFiles.cs"), "RepoFiles.cs", name),
            Entry(Path.Combine(architecture, "StructureTests.cs"), "StructureTests.cs", name),
            Entry(Path.Combine(architecture, "ConventionTests.cs"), "ConventionTests.cs", name),
            Entry(Path.Combine(architecture, "EndpointTests.cs"), "EndpointTests.cs", name),
        ];
    }

    private static KeyValuePair<string, string> Entry(string path, string template, string name) =>
        new(path, Read(template).Replace(Token, name, StringComparison.Ordinal));

    /// <summary>
    /// Reads one embedded template.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning empty. A scaffold that silently wrote nothing for one file would
    /// produce a solution that does not compile, and the missing resource is the thing worth naming.
    /// </remarks>
    private static string Read(string template)
    {
        string resource = "Sdlc.Core.Init.Scaffold." + template + ".template";

        using Stream? stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(resource);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"The scaffold template '{resource}' is not embedded in this build. Every file in "
                + "Init/Scaffold has to be an EmbeddedResource, and Sdlc.Core.csproj includes them by "
                + "wildcard - so this means a renamed file or a changed root namespace.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
