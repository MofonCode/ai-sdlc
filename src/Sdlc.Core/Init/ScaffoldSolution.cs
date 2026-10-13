using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>
/// Writes the solution a scaffolded repository starts as, once a plan for it has been approved.
/// </summary>
/// <remarks>
/// <para><strong>Separate from <c>init</c>, and gated — D-35.</strong> Adoption writes the files that
/// constrain how code is written and stops there. The solution file, the projects, the architecture
/// tests and the reference slice are code, and code in a governed repository arrives behind an intent,
/// a spec, a plan and an approval. Writing them at adoption made the scaffold the only thing in the
/// repository with no chain behind it, which is the promise the whole design is built on.</para>
///
/// <para><strong>The approval is what permits it.</strong> This dispatches the same
/// <see cref="ApprovalGate"/> query the build stage does, so a refusal here is the same refusal with
/// the same reason. A plan declaring these files has therefore been approved, and the
/// declared-against-changed comparison then holds the build to it.</para>
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

    /// <summary>Asks for the solution skeleton to be written.</summary>
    /// <param name="Id">The cycle whose approved plan declares it.</param>
    public sealed record Command(CycleId Id) : IRequest<Response>;

    /// <summary>What was written, and what was already there.</summary>
    /// <param name="Name">The solution's name, derived from the repository's directory.</param>
    /// <param name="Written">The files written, relative to the solution root.</param>
    /// <param name="Kept">The files already present and therefore left alone.</param>
    public sealed record Response(
        string Name,
        IReadOnlyList<string> Written,
        IReadOnlyList<string> Kept);

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

            // An adopted solution keeps its own shape. A skeleton written into a repository that
            // already has one is noise at best and a second competing layout at worst.
            //
            // Origin rather than standard, and deliberately: this asks whether the repository already
            // has a layout, which is a fact about where the code came from. --adopt-standard says which
            // conventions new work follows and does not conjure an absent solution file, four projects
            // and a reference slice into a codebase that has its own -- so a solution on
            // standard: profile is still refused here, and the refusal below is still the right advice.
            if (read.Value.Origin != SolutionOrigin.Scaffolded)
            {
                return Result.Fail<Response>(
                    "This solution's origin is 'adopted', so it already has a shape and this would "
                    + "impose a second one. The skeleton is for a solution being started, and a plan "
                    + "that wants to move an adopted solution onto the profile's layout argues for it "
                    + "like any other departure.");
            }

            // Nothing here asks what the solution declares it is built from, and that is deliberate. A
            // profile name used to answer it -- one that shipped no skeleton was refused -- and there is
            // no profile to ask any more (D-76). What stands in its place is the approval below: this
            // skeleton is .NET, a plan that declares these files says so, and a person approved that
            // plan. Where the declared stack pins something else, the same plan had to argue the
            // departure to get there, which is CON_15 doing the work the flag did.
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
                    $"The gate refuses cycle {request.Id}, so there is no approved plan for this skeleton. {gate.Value.Explanation}"));
            }

            string name = Scaffold.NameFrom(Path.GetFileName(solution.Path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)));

            List<string> written = [];
            List<string> kept = [];

            // The files that constrain how the code is written arrive with the code they constrain.
            // Adoption wrote them, when adoption was told a profile; it is told nothing about the stack
            // now, and they do nothing until there is a project to apply to -- so nothing is lost by
            // their arriving here, and a solution on another stack never gets five .NET files to delete
            // (D-76). First, because the skeleton does not compile under the policy a consumer gets
            // without them.
            IEnumerable<KeyValuePair<string, string>> files =
            [
                .. Initialise.StackFiles().Select(file => KeyValuePair.Create(file.Path, file.Contents)),
                .. Scaffold.Files(name),
            ];

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

            return Result.Ok(new Response(name, written, kept));
        }
    }
}
