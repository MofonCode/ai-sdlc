using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>
/// Writes the solution a scaffolded repository starts as, or the build settings an adopted one's plan
/// declares, once that plan has been approved.
/// </summary>
/// <remarks>
/// <para><strong>Separate from <c>init</c>, and gated — D-35.</strong> Adoption writes the files that
/// govern how a change is made and stops there. The solution file, the projects, the architecture
/// tests and the reference slice are code, and code in a governed repository arrives behind an intent,
/// a spec, a plan and an approval. Writing them at adoption made the scaffold the only thing in the
/// repository with no chain behind it, which is the promise the whole design is built on.</para>
///
/// <para><strong>The approval is what permits it.</strong> This dispatches the same
/// <see cref="ApprovalGate"/> query the build stage does, so a refusal here is the same refusal with
/// the same reason. A plan declaring these files has therefore been approved, and the
/// declared-against-changed comparison then holds the build to it.</para>
///
/// <para><strong>Two things it writes, to two kinds of solution.</strong> A scaffolded one gets the
/// skeleton and the build settings it compiles under, together. An adopted one never gets the
/// skeleton -- it has a shape already -- and gets exactly the build settings its approved plan
/// declares. That second half is what <c>sdlc init --adopt-standard</c> used to do at adoption, with
/// no cycle behind it: <c>Directory.Build.props</c> makes warnings errors, so the file arriving is the
/// moment a build can fail on code nobody touched, and that belongs inside the declared scope of one
/// cycle a person approved (D-77).</para>
///
/// <para><strong>A command rather than templates carried in the plan.</strong> Prose defaults reach a
/// plan by being injected into it, because a session inside a governed solution cannot read a file
/// that lives in the plugin. Files are different: transcribing several hundred lines through a
/// document invites corruption and proves nothing, so the binary writes them and the gate decides
/// whether it may.</para>
///
/// <para>It writes only what is absent. A solution that already has these files has had this run, or
/// has grown past the skeleton, and either way overwriting is the one thing a scaffold must not do.</para>
/// </remarks>
public static class ScaffoldSolution
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddScaffoldSolutionRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for the solution skeleton, or a plan's build settings, to be written.</summary>
    /// <param name="Id">The cycle whose approved plan declares it.</param>
    public sealed record Command(CycleId Id) : IRequest<Response>;

    /// <summary>What was written, and what was already there.</summary>
    /// <param name="Name">The solution's name, derived from the repository's directory.</param>
    /// <param name="Written">The files written, relative to the solution root.</param>
    /// <param name="Kept">The files already present and therefore left alone.</param>
    /// <param name="Skeleton">
    /// Whether the skeleton was what this wrote. False for an adopted solution, which gets the build
    /// settings its plan declares and nothing else -- so the report does not describe a reference slice
    /// that is not there.
    /// </param>
    public sealed record Response(
        string Name,
        IReadOnlyList<string> Written,
        IReadOnlyList<string> Kept,
        bool Skeleton = true);

    private sealed class Handler(SolutionRoot solution, Mediator mediator)
        : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<SolutionDocument> read = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            // Nothing here asks what the solution declares it is built from, and that is deliberate. A
            // profile name used to answer it -- one that shipped no skeleton was refused -- and there is
            // no profile to ask any more (D-76). What stands in its place is the approval below: these
            // files are .NET, a plan that declares them says so, and a person approved that plan. Where
            // the declared stack pins something else, or switches a build-settings row off, the same
            // plan had to argue the departure to get there, which is CON_15 doing the work a flag did.
            //
            // The same question the build stage asks, so a refusal here reads identically. This is
            // what keeps the first code in the repository behind an approved plan.
            Result<ApprovalGate.Response> gate = await mediator
                .Send<ApprovalGate.Query, ApprovalGate.Response>(
                    new ApprovalGate.Query(request.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (gate.IsFailed)
            {
                return gate.ToResult<Response>();
            }

            if (gate.Value.Refusal != ApprovalGate.GateRefusal.None)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The gate refuses cycle {request.Id}, so there is no approved plan for this to write from. {gate.Value.Explanation}"));
            }

            string name = Scaffold.NameFrom(Path.GetFileName(solution.Path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)));

            bool skeleton = read.Value.Origin == SolutionOrigin.Scaffolded;

            List<KeyValuePair<string, string>> files = skeleton
                ? Skeleton(name)
                : await Declared(request.Id, cancellationToken).ConfigureAwait(false);

            // An adopted solution keeps its own shape. A skeleton written into a repository that
            // already has one is noise at best and a second competing layout at worst.
            //
            // Origin rather than standard, and deliberately: this asks whether the repository already
            // has a layout, which is a fact about where the code came from. A solution on
            // standard: profile is held to the profile's conventions for new work, and that does not
            // conjure an absent solution file, four projects and a reference slice into a codebase
            // that has its own. What an adopted solution can be given is the build settings, and only
            // the ones its plan names.
            if (files.Count == 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This solution's origin is 'adopted', so it already has a shape and the skeleton would impose a second one. What this writes for an adopted solution is the build settings its approved plan declares -- {string.Join(", ", BuildSettings.All.Select(setting => setting.Path))} -- and the plan of cycle {request.Id} declares none of them. 'sdlc stack' shows which ones the declared stack takes and which are missing."));
            }

            List<string> written = [];
            List<string> kept = [];

            foreach ((string relative, string contents) in files)
            {
                string path = Path.Combine(solution.Path, relative);

                if (File.Exists(path))
                {
                    kept.Add(relative);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, contents, cancellationToken).ConfigureAwait(false);
                written.Add(relative);
            }

            return Result.Ok(new Response(name, written, kept, skeleton));
        }

        /// <summary>
        /// What a scaffolded solution starts as: the build settings, then the skeleton.
        /// </summary>
        /// <remarks>
        /// The files that constrain how the code is written arrive with the code they constrain. They
        /// do nothing until there is a project to apply to, so nothing is lost by their arriving here,
        /// and a solution on another stack never gets five .NET files to delete (D-76). All five,
        /// whatever the declared stack's rows say, and first: the skeleton does not compile under the
        /// policy a consumer gets without them.
        /// </remarks>
        /// <param name="name">The solution's name.</param>
        /// <returns>Each file's path relative to the solution root, and its contents.</returns>
        private static List<KeyValuePair<string, string>> Skeleton(string name) =>
        [
            .. BuildSettings.All.Select(setting => KeyValuePair.Create(setting.OnDisk, setting.Contents)),
            .. Scaffold.Files(name),
        ];

        /// <summary>
        /// The build settings an adopted solution's plan declares.
        /// </summary>
        /// <remarks>
        /// Read from the plan rather than from the declared stack's rows, because the plan is what a
        /// person approved for this cycle and the rows are what the solution intends in general. A row
        /// switched on with no plan behind it would otherwise let any approved cycle, about anything,
        /// write a file that makes warnings errors -- and that file would reach the cycle's diff as one
        /// its plan never declared.
        /// </remarks>
        /// <param name="id">The cycle.</param>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>Each declared file's path relative to the solution root, and its contents.</returns>
        private async Task<List<KeyValuePair<string, string>>> Declared(
            CycleId id,
            CancellationToken cancellationToken)
        {
            string plan = new Cycle(id, solution.Path).PathOf(ArtifactKind.Plan);

            IReadOnlyList<string> declared = DeclaredFiles.Read(
                await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false));

            return
            [
                .. BuildSettings.All
                    .Where(setting => declared.Contains(setting.Path, StringComparer.OrdinalIgnoreCase))
                    .Select(setting => KeyValuePair.Create(setting.OnDisk, setting.Contents)),
            ];
        }
    }
}
