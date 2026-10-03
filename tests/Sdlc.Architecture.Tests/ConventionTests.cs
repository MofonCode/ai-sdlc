using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core;

namespace Sdlc.Architecture.Tests;

/// <summary>
/// Rules about the code itself, read from the compiled assemblies.
/// </summary>
/// <remarks>
/// Reflection rather than source text, because these are questions about types and a regular
/// expression over source answers a different question that looks the same. The cost is that this
/// project has to reference what it inspects; the rules in <see cref="StructureTests"/> are the ones
/// that must not, and they read files instead.
/// <para>
/// Both rules assert they actually inspected something. A reflection rule whose assembly failed to
/// load enumerates nothing, finds no violations and passes — which is the same vacuous success
/// <c>RepoFiles</c> is written against, arriving through a different mechanism.
/// </para>
/// </remarks>
[TestClass]
public sealed class ConventionTests
{
    /// <summary>
    /// Types the sealed rule does not apply to, each for a stated reason.
    /// </summary>
    /// <remarks>
    /// <c>Program</c> is synthesised by the compiler from the top-level statements in
    /// <c>Program.cs</c>. Whether it carries <see cref="CompilerGeneratedAttribute"/> is a detail of
    /// the compiler rather than a promise, so it is named here instead of being relied upon to filter
    /// itself out.
    /// </remarks>
    private static readonly string[] NotSubjectToSealing = ["Program"];

    private static IReadOnlyList<Assembly> Assemblies => [CoreAssembly, CliAssembly];

    private static Assembly CoreAssembly => typeof(SdlcServices).Assembly;

    /// <summary>
    /// The gate binary.
    /// </summary>
    /// <remarks>
    /// Loaded by name rather than through <c>typeof</c>, because every type in it is internal and
    /// this project is not on its <c>InternalsVisibleTo</c> list — which is the behaviour the
    /// application wants, and which the rule below is partly there to keep. The project reference
    /// puts the file in this assembly's directory, so the load resolves.
    /// </remarks>
    private static Assembly CliAssembly => Assembly.Load(new AssemblyName("sdlc"));

    /// <summary>
    /// Every class is sealed, static or abstract.
    /// </summary>
    /// <remarks>
    /// An unsealed class is an extension point, and an extension point nobody asked for is a
    /// decision made by omission. Sealing by default means inheritance is something a person wrote
    /// down, and it is also what lets the runtime devirtualise.
    /// <para>
    /// No analyzer covers this. <c>CA1852</c> reaches internal types only, and ships at a severity
    /// that warnings-as-errors does not promote. So the rule lives here, and it was written at the
    /// moment the repository already satisfied it — which is the only time adopting a rule is free.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Every_class_is_sealed_static_or_abstract()
    {
        List<string> offenders = [];
        int inspected = 0;

        foreach (Assembly assembly in Assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (!type.IsClass || IsCompilerArtefact(type) || NotSubjectToSealing.Contains(type.Name))
                {
                    continue;
                }

                inspected++;

                // A static class is abstract and sealed; an abstract class is abstract and not. Both
                // are deliberate shapes, and neither can be instantiated by accident.
                if (type.IsSealed || type.IsAbstract)
                {
                    continue;
                }

                offenders.Add(type.FullName ?? type.Name);
            }
        }

        Assert.IsTrue(
            inspected > 0,
            "This rule inspected no types at all, which means it would pass however the code looked. "
            + "The likely cause is an assembly that did not load — check that the project references "
            + "are intact and that the gate binary is beside this assembly.");

        Assert.AreEqual(
            0,
            offenders.Count,
            string.Create(
                CultureInfo.InvariantCulture,
                $"These classes are neither sealed, static nor abstract (of {inspected} inspected):\n  ")
            + string.Join("\n  ", offenders)
            + "\n\nSeal it, or make it static or abstract. If something genuinely needs to be derived "
            + "from, that is a decision worth writing down: add it to NotSubjectToSealing with the "
            + "reason, in the same change that unseals it.");
    }

    /// <summary>
    /// Every private field is <c>_camelCase</c>, and every constant is PascalCase.
    /// </summary>
    /// <remarks>
    /// <para><strong>.editorconfig says this and enforces nothing.</strong> Naming surfaces as
    /// IDE1006, which <c>EnforceCodeStyleInBuild</c> does not cover - it is an IDE-only diagnostic.
    /// Fifteen fields in this repository broke the rule while the build reported zero warnings, which
    /// is the "appears enforced and is not" failure sitting inside the file that is meant to be the
    /// compiler layer. This is what makes it real.</para>
    ///
    /// <para>The prefix is the point rather than the capitalisation: a reader scanning a method body
    /// can tell an assignment to the instance from an assignment to a local without checking a
    /// declaration. A constant is exempt because it is a value rather than state, and the code
    /// already reads that way.</para>
    /// </remarks>
    [TestMethod]
    public void Every_private_field_is_underscore_camel_case_and_every_constant_is_pascal()
    {
        List<string> offenders = [];
        int inspected = 0;

        foreach (Assembly assembly in Assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (IsCompilerArtefact(type))
                {
                    continue;
                }

                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    // A primary constructor's capture, an auto-property's backing store and a
                    // closure's state are all emitted rather than written, and they carry a bracket
                    // that is not legal in an identifier.
                    if (field.Name.Contains('<', StringComparison.Ordinal)
                        || field.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
                    {
                        continue;
                    }

                    inspected++;
                    string where = (type.FullName ?? type.Name) + "." + field.Name;

                    if (field.IsLiteral)
                    {
                        if (!char.IsUpper(field.Name[0]))
                        {
                            offenders.Add(where + " is a constant and should be PascalCase");
                        }

                        continue;
                    }

                    if (!field.Name.StartsWith('_') || field.Name.Length < 2 || !char.IsLower(field.Name[1]))
                    {
                        offenders.Add(where + " should be _camelCase");
                    }
                }
            }
        }

        Assert.IsTrue(
            inspected > 0,
            "This rule inspected no fields, which means it would pass however they were named. The "
            + "likely cause is an assembly that did not load.");

        Assert.AreEqual(
            0,
            offenders.Count,
            string.Create(
                CultureInfo.InvariantCulture,
                $"These fields do not follow the naming rules (of {inspected} inspected):\n  ")
            + string.Join("\n  ", offenders)
            + "\n\nThe rule is in .editorconfig and nothing in the build enforces it, which is why it "
            + "is here. Rename the field, or - if a whole area is being adopted rather than written - "
            + "the honest move is an allow-list in this file that shrinks, not a rule that is deleted.");
    }

    /// <summary>
    /// Every request the gate declares can actually be dispatched.
    /// </summary>
    /// <remarks>
    /// A request type with no registered handler compiles, reads as an operation, and throws the
    /// first time anything sends it. Worse, it may never be sent at all — in which case it is a
    /// declared operation the application cannot perform, and nothing but this rule notices.
    /// <para>
    /// The container is the real one, built through <see cref="SdlcServices.AddSdlc"/>. Asserting
    /// against a hand-written list of registrations would test the list.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Every_request_the_core_declares_has_a_registered_handler()
    {
        List<(Type Request, Type Response)> requests = [];

        foreach (Type type in CoreAssembly.GetTypes())
        {
            if (IsCompilerArtefact(type) || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (Type contract in type.GetInterfaces())
            {
                if (contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IRequest<>))
                {
                    requests.Add((type, contract.GetGenericArguments()[0]));
                }
            }
        }

        Assert.IsTrue(
            requests.Count > 0,
            "This rule found no request types, so it would pass with every handler unregistered. "
            + "Either the core assembly did not load, or the mediator's request interface moved.");

        string library = Path.Combine(RepoFiles.FindRepoRoot(), "CONSTRAINTS.md");
        string solutionRoot = Path.Combine(Path.GetTempPath(), "sdlc-architecture-" + Guid.NewGuid().ToString("N"));

        ServiceProvider provider = new ServiceCollection()
            .AddSdlc(library, solutionRoot)
            .BuildServiceProvider();

        List<string> unregistered = [];

        using (provider)
        {
            foreach ((Type request, Type response) in requests)
            {
                Type handler = typeof(IRequestHandler<,>).MakeGenericType(request, response);

                if (provider.GetService(handler) is null)
                {
                    unregistered.Add(Describe(request) + " -> " + Describe(response));
                }
            }
        }

        Assert.AreEqual(
            0,
            unregistered.Count,
            string.Create(
                CultureInfo.InvariantCulture,
                $"These requests have no handler registered in AddSdlc (of {requests.Count} declared):\n  ")
            + string.Join("\n  ", unregistered)
            + "\n\nRegister the handler, or delete the request. A request nothing can dispatch is an "
            + "operation the application declares and cannot perform — and if the work is really done "
            + "by a static method called from one place, it is not an application operation and the "
            + "request type is what should go.");
    }

    /// <summary>
    /// A type's name as a reader would say it, with its holder when it is nested.
    /// </summary>
    /// <remarks>
    /// Every request this library declares is nested in its operation's holder, and all but a handful
    /// are called <c>Query</c> or <c>Command</c> — so a bare name told a reader which rule failed and
    /// nothing at all about where. <c>EffectiveConstraints.Query</c> is the shortest string that
    /// identifies one, and the failure this rule reports is a type somebody then has to go and find.
    /// </remarks>
    /// <param name="type">The type to name.</param>
    /// <returns>Its name, prefixed with its holder's when it has one.</returns>
    private static string Describe(Type type) =>
        type.DeclaringType is { } holder ? holder.Name + "." + type.Name : type.Name;

    /// <summary>
    /// Whether a type was emitted by the compiler rather than written.
    /// </summary>
    /// <remarks>
    /// Checks the attribute and the name. Closure and iterator types carry angle brackets that are
    /// not legal in C# identifiers, so a name containing one was never typed by a person; the
    /// attribute catches the rest.
    /// </remarks>
    private static bool IsCompilerArtefact(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
        || type.Name.Contains('<', StringComparison.Ordinal)
        || type.DeclaringType is { } outer && IsCompilerArtefact(outer);
}
