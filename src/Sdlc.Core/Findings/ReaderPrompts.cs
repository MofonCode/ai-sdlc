using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Perspectives;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Writes every reader's prompt for one round (D-117).
/// </summary>
/// <remarks>
/// <para><strong>The agent used to compose these from the skills' prose</strong>, pasting files by hand
/// and at whatever context its session had reached, so no two rounds were judged from the same text and
/// one session wrote throwaway scripts to do it. Which files, in which order, under which headings is
/// deterministic, so it is the binary's. The judging stays in a fresh process the skill starts: D-58
/// kept the binary away from the verdict, and this does not go near it.</para>
/// <para><strong>The shared block first, byte for byte the same for every reader of the round</strong> —
/// the stack file on a plan, the document above, the spec and the evidence on a verify, then the document
/// — and each reader's own part last. A prefix the readers share is one a host can cache, and one a
/// replay can compare. The rules are in the rules reader's part rather than the shared block, because a
/// perspective is not handed the constraint set and putting it there would change what it judges.</para>
/// </remarks>
public static class ReaderPrompts
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReaderPromptsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>One prompt written.</summary>
    /// <param name="Reader">Which reader it is for.</param>
    /// <param name="Path">Where it was written.</param>
    /// <param name="Bytes">Its size.</param>
    public sealed record Written(string Reader, string Path, long Bytes);

    /// <summary>What was written, and which readers the round needs.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Artifact">The artifact being judged.</param>
    /// <param name="Directory">The round's directory.</param>
    /// <param name="Readers">Every reader this round runs blind, in order: the rules reader, then each perspective.</param>
    /// <param name="Prompts">The prompts written; empty when only the list was asked for.</param>
    /// <param name="Unwritten">Active perspectives with no document yet, which have nothing to run.</param>
    public sealed record Response(
        Cycle Cycle,
        ArtifactKind Artifact,
        string Directory,
        IReadOnlyList<string> Readers,
        IReadOnlyList<Written> Prompts,
        IReadOnlyList<string> Unwritten);

    /// <summary>Writes one round's prompts.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Artifact">The artifact being judged.</param>
    /// <param name="Directory">Where to write them: a directory outside the repository.</param>
    /// <param name="Reader">
    /// One reader only — <c>rules</c>, a perspective's name, <c>reconcile</c> or <c>adjudicate</c> — or
    /// null for every reader that runs blind.
    /// </param>
    /// <param name="List">Write nothing; only say which readers the round needs.</param>
    public sealed record Command(
        CycleId Id,
        ArtifactKind Artifact,
        string Directory,
        string? Reader = null,
        bool List = false) : IRequest<Response>;

    private const string Prefix = "Sdlc.Core.Prompts.";

    private static readonly JsonSerializerOptions _listing = new(ReaderAnswers.Options)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>One embedded template, by name.</summary>
    /// <param name="name">The template's file name without its extension.</param>
    /// <returns>Its text, with line endings normalised so a prompt is the same bytes on every platform.</returns>
    public static string Template(string name)
    {
        using Stream stream = typeof(ReaderPrompts).Assembly.GetManifestResourceStream(Prefix + name + ".md")
            ?? throw new InvalidOperationException("The binary carries no prompt template called " + name + ".");
        using StreamReader reader = new(stream);

        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>
    /// The block every reader of a round shares, in one order. Without the document when the reader is the
    /// adjudicator, which writes it.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="artifact">The artifact being judged.</param>
    /// <param name="withDocument">Whether to end with the document itself.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The block.</returns>
    public static async Task<string> Shared(Cycle cycle, ArtifactKind artifact, bool withDocument, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        StringBuilder block = new();
        block.Append("# What every reader of this round is given\n\n");

        // The stack file is a rule source, judged by CON_15 on a plan alone (D-58). An absent one is said
        // to be absent: a reader handed nothing cannot tell "declares nothing" from "nobody showed me".
        if (artifact == ArtifactKind.Plan)
        {
            string stack = Path.Combine(cycle.SolutionRoot, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
            block.Append("What this solution declares it is built from:\n\n");
            await File(block, ".sdlc/" + Cycle.TechStackFileName, stack, "This solution has no techstack.md: it declares nothing.", cancellationToken).ConfigureAwait(false);
        }

        // A verify's document above is the plan, which commits to the proof per criterion.
        ArtifactKind? above = artifact == ArtifactKind.Verify ? ArtifactKind.Plan : Cycle.UpstreamOf(artifact);

        block.Append("The document above it in the chain, which it is meant to be derived from:\n\n");

        if (above is { } upstream)
        {
            await File(block, Cycle.FileNameOf(upstream), cycle.PathOf(upstream), "It does not exist yet.", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            block.Append("None -- this is the first artifact.\n\n");
        }

        // What CON_22 is decided against: the criteria, and every piece of evidence the verification may
        // cite. The build log where this checkout has one; it is not committed (D-92).
        if (artifact == ArtifactKind.Verify)
        {
            block.Append("The criteria it adjudicates, and the evidence it may cite:\n\n");
            await File(block, "spec.md", cycle.PathOf(ArtifactKind.Spec), "It does not exist.", cancellationToken).ConfigureAwait(false);

            foreach (string name in new[] { "tests.json", "base.sha", "build.log", "diff.patch" })
            {
                string path = Path.Combine(cycle.EvidenceDirectory, name);

                if (name == "build.log" && !System.IO.File.Exists(path))
                {
                    continue;
                }

                await File(block, "evidence/" + name, path, "It was not recorded.", cancellationToken).ConfigureAwait(false);
            }
        }

        if (withDocument)
        {
            block.Append("The document to judge:\n\n");
            await File(block, Cycle.FileNameOf(artifact), cycle.PathOf(artifact), "It does not exist.", cancellationToken).ConfigureAwait(false);
        }

        return block.ToString();
    }

    /// <summary>Findings as the reconciler reads them: one JSON array, the fields that say what each is.</summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The array.</returns>
    internal static string Listing(IEnumerable<Finding> findings) => JsonSerializer.Serialize(
        findings.Select(finding => new
        {
            finding.Id,
            Severity = finding.Severity == Severity.Blocking ? "blocking" : "suggestion",
            finding.Perspective,
            finding.Cites,
            finding.Statement,
            finding.Locus,
        }),
        _listing);

    private static async Task File(StringBuilder block, string name, string path, string absent, CancellationToken cancellationToken)
    {
        block.Append("<file name=\"").Append(name).Append("\">\n");

        if (System.IO.File.Exists(path))
        {
            string text = (await System.IO.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).ReplaceLineEndings("\n");
            block.Append(text);

            if (!text.EndsWith('\n'))
            {
                block.Append('\n');
            }
        }
        else
        {
            block.Append(absent).Append('\n');
        }

        block.Append("</file>\n\n");
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string name = Cycle.FileNameOf(request.Artifact);

            if (!cycle.Exists)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no directory at {cycle.Directory}. 'sdlc cycles' lists every cycle this solution holds."));
            }

            bool adjudicating = string.Equals(request.Reader, ReaderAnswers.Adjudicate, StringComparison.Ordinal);

            // A verify's readers quote the patch, and a fresh clone of the build has none: it is not committed
            // (D-124). Rebuilt from the two commits the evidence names, so they read the patch the build did.
            if (request.Artifact == ArtifactKind.Verify)
            {
                await EvidenceDiff.Restore(cycle, repository, solution.Path, cancellationToken).ConfigureAwait(false);
            }

            if (adjudicating && request.Artifact != ArtifactKind.Verify)
            {
                return Result.Fail<Response>("The adjudicator writes a verify document, so it is composed for 'verify' and nothing else.");
            }

            if (!adjudicating && !System.IO.File.Exists(cycle.PathOf(request.Artifact)))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no {name}. A round judges a document that exists."));
            }

            Result<ActivePerspectives.Response> active = await mediator
                .Send<ActivePerspectives.Query, ActivePerspectives.Response>(new ActivePerspectives.Query(request.Id), cancellationToken)
                .ConfigureAwait(false);

            if (active.IsFailed)
            {
                return active.ToResult<Response>();
            }

            List<string> readers = [ReaderAnswers.Rules, .. active.Value.Perspectives.Where(p => p.Written).Select(p => p.Name)];
            List<string> unwritten = [.. active.Value.Perspectives.Where(p => !p.Written).Select(p => p.Name)];

            if (request.List)
            {
                return Result.Ok(new Response(cycle, request.Artifact, request.Directory, readers, [], unwritten));
            }

            IReadOnlyList<string> wanted = request.Reader is null ? readers : [request.Reader];

            if (request.Reader is { } one
                && one != ReaderAnswers.Reconcile
                && !adjudicating
                && !readers.Contains(one, StringComparer.Ordinal))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{one}' is not a reader of this round. It runs: {string.Join(", ", readers)}; then {ReaderAnswers.Reconcile}{(request.Artifact == ArtifactKind.Verify ? ", and before the round " + ReaderAnswers.Adjudicate : string.Empty)}."));
            }

            System.IO.Directory.CreateDirectory(request.Directory);

            // Every reader of the round from scratch clears what an earlier round left there, so an answer
            // from last time cannot be collected as this one's.
            if (request.Reader is null)
            {
                foreach (string stale in readers.Append(ReaderAnswers.Reconcile))
                {
                    System.IO.File.Delete(ReaderAnswers.PromptPath(request.Directory, stale));
                    System.IO.File.Delete(ReaderAnswers.AnswerPath(request.Directory, stale));
                }

                System.IO.File.Delete(Path.Combine(request.Directory, ReaderAnswers.RoundFileName));
            }

            string shared = await Shared(cycle, request.Artifact, withDocument: !adjudicating, cancellationToken).ConfigureAwait(false);
            List<Written> written = [];

            foreach (string reader in wanted)
            {
                Result<string> composed = reader switch
                {
                    ReaderAnswers.Rules => await ForRules(request, shared, cancellationToken).ConfigureAwait(false),
                    ReaderAnswers.Reconcile => await ForReconciler(cycle, request, readers, cancellationToken).ConfigureAwait(false),
                    ReaderAnswers.Adjudicate => await ForAdjudicator(cycle, request, shared, readers, cancellationToken).ConfigureAwait(false),
                    _ => await ForPerspective(request, reader, shared, cancellationToken).ConfigureAwait(false),
                };

                if (composed.IsFailed)
                {
                    return composed.ToResult<Response>();
                }

                string path = ReaderAnswers.PromptPath(request.Directory, reader);
                await System.IO.File.WriteAllTextAsync(path, composed.Value, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                written.Add(new Written(reader, path, new FileInfo(path).Length));
            }

            return Result.Ok(new Response(cycle, request.Artifact, request.Directory, readers, written, unwritten));
        }

        private async Task<Result<string>> Constraints(ArtifactKind artifact, CancellationToken cancellationToken)
        {
            string name = Cycle.NameOf(artifact);
            Result<EffectiveConstraints.Response> set = await mediator
                .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(name), cancellationToken)
                .ConfigureAwait(false);

            return set.IsFailed
                ? set.ToResult<string>()
                : Result.Ok(ConstraintRenderer.Render(set.Value, ConstraintDetail.Full, name).ReplaceLineEndings("\n").TrimEnd());
        }

        private async Task<Result<string>> ForRules(Command request, string shared, CancellationToken cancellationToken)
        {
            Result<string> rules = await Constraints(request.Artifact, cancellationToken).ConfigureAwait(false);

            if (rules.IsFailed)
            {
                return rules;
            }

            Result<EffectiveConstraints.Response> set = await mediator
                .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(Cycle.NameOf(request.Artifact)), cancellationToken)
                .ConfigureAwait(false);

            if (set.IsFailed)
            {
                return set.ToResult<string>();
            }

            (string rows, string rowsShape) = JudgedItems.Prompt(
                await JudgedItems.In(new Cycle(request.Id, solution.Path), set.Value, cancellationToken).ConfigureAwait(false));

            // A Met is checked by finding its quote in the diff, and a reader without that instruction
            // only checks that something was named.
            string verify = request.Artifact == ArtifactKind.Verify
                ? "\nEach Met quotes the assertion it rests on from `diff.patch`. Find each quote in the diff, and ask which\ncase the criterion states the test never runs, and which outcome it never reads. A Met whose quote is\nmissing, is not in the diff, or asserts less than the criterion asks breaks `CON_22`. A defect you can only\nimagine the test missing is a suggestion.\n"
                : string.Empty;

            return Result.Ok(shared + Template("rules")
                .Replace("{{constraints}}", rules.Value, StringComparison.Ordinal)
                .Replace("{{verify}}", verify, StringComparison.Ordinal)
                .Replace("{{rows}}", rows, StringComparison.Ordinal)
                .Replace("{{rowsShape}}", rowsShape, StringComparison.Ordinal));
        }

        private async Task<Result<string>> ForPerspective(Command request, string reader, string shared, CancellationToken cancellationToken)
        {
            Result<Perspective> perspective = PerspectiveLibrary.Find(reader);

            if (perspective.IsFailed)
            {
                return perspective.ToResult<string>();
            }

            // After the first round a perspective raises suggestions about what moved and nothing else: a
            // suggestion about unmoved text is one an earlier round already had the chance to raise.
            Result<ChangedSections.Response> changed = await mediator
                .Send<ChangedSections.Query, ChangedSections.Response>(new ChangedSections.Query(request.Id, request.Artifact), cancellationToken)
                .ConfigureAwait(false);

            if (changed.IsFailed)
            {
                return changed.ToResult<string>();
            }

            string since = changed.Value.Since is null
                ? string.Empty
                : changed.Value.Changed.Count == 0
                    ? "\nThis document has been read before, and no section has changed since its last round. Raise no\nsuggestions. A blocking finding is not limited: a rule broken anywhere is still broken.\n"
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"\nThis document has been read before. Since its round {changed.Value.Since}, these sections changed:\n\n{string.Join("\n", changed.Value.Changed.Select(heading => "- " + heading))}\n\nRaise suggestions about those sections only. A blocking finding is not limited: a rule broken\nanywhere is still broken.\n");

            string method = ActivePerspectives.RenderStage(perspective.Value, Cycle.NameOf(request.Artifact), cover: false)
                .ReplaceLineEndings("\n")
                .TrimEnd();

            return Result.Ok(shared + Template("perspective")
                .Replace("{{perspective}}", method, StringComparison.Ordinal)
                .Replace("{{changed}}", since, StringComparison.Ordinal)
                .Replace("{{name}}", perspective.Value.Name, StringComparison.Ordinal));
        }

        private async Task<Result<string>> ForReconciler(Cycle cycle, Command request, IReadOnlyList<string> readers, CancellationToken cancellationToken)
        {
            List<Finding> raised = [];

            foreach (string reader in readers)
            {
                Result<ReaderAnswer> answer = await ReaderAnswers.Read(request.Directory, reader, cancellationToken).ConfigureAwait(false);

                if (answer.IsFailed)
                {
                    return answer.ToResult<string>();
                }

                Perspective? perspective = reader == ReaderAnswers.Rules ? null : PerspectiveLibrary.Find(reader).ValueOrDefault;
                Result<ReadBody> body = ReaderAnswers.FindingsOf(answer.Value, perspective);

                if (body.IsFailed)
                {
                    return body.ToResult<string>();
                }

                raised.AddRange(body.Value.Findings);
            }

            Result<ReadFindings.Response> standing = await mediator
                .Send<ReadFindings.Query, ReadFindings.Response>(new ReadFindings.Query(request.Id, request.Artifact), cancellationToken)
                .ConfigureAwait(false);

            if (standing.IsFailed)
            {
                return standing.ToResult<string>();
            }

            Result<Suggestions.Response> suggestions = await mediator
                .Send<Suggestions.Query, Suggestions.Response>(new Suggestions.Query(request.Id), cancellationToken)
                .ConfigureAwait(false);

            if (suggestions.IsFailed)
            {
                return suggestions.ToResult<string>();
            }

            string text = (await System.IO.File.ReadAllTextAsync(cycle.PathOf(request.Artifact), cancellationToken).ConfigureAwait(false))
                .ReplaceLineEndings("\n")
                .TrimEnd();

            return Result.Ok(Template("reconcile")
                .Replace("{{document}}", Cycle.FileNameOf(request.Artifact), StringComparison.Ordinal)
                .Replace("{{text}}", text, StringComparison.Ordinal)
                .Replace("{{round}}", Listing(raised), StringComparison.Ordinal)
                .Replace("{{standing}}", FindingsRenderer.Render(standing.Value).ReplaceLineEndings("\n").TrimEnd(), StringComparison.Ordinal)
                .Replace("{{suggestions}}", SuggestionsRenderer.Render(suggestions.Value).ReplaceLineEndings("\n").TrimEnd(), StringComparison.Ordinal));
        }

        private async Task<Result<string>> ForAdjudicator(
            Cycle cycle,
            Command request,
            string shared,
            IReadOnlyList<string> readers,
            CancellationToken cancellationToken)
        {
            Result<string> rules = await Constraints(ArtifactKind.Verify, cancellationToken).ConfigureAwait(false);

            if (rules.IsFailed)
            {
                return rules;
            }

            StringBuilder perspectives = new();

            foreach (string reader in readers.Where(reader => reader != ReaderAnswers.Rules))
            {
                Result<Perspective> perspective = PerspectiveLibrary.Find(reader);

                if (perspective.IsFailed)
                {
                    return perspective.ToResult<string>();
                }

                perspectives
                    .Append("<perspective name=\"").Append(reader).Append("\">\n")
                    .Append(ActivePerspectives.RenderStage(perspective.Value, "verify", cover: false).ReplaceLineEndings("\n").TrimEnd())
                    .Append("\n</perspective>\n\n");
            }

            // A blocking finding against the verdict is answered by a new adjudication handed the findings,
            // never by an edit in the session (D-109). So when there is a verdict with blockers open, the
            // adjudicator reads both and answers each from the evidence.
            string previous = string.Empty;
            string current = cycle.PathOf(ArtifactKind.Verify);

            Result<ReadFindings.Response> standing = await mediator
                .Send<ReadFindings.Query, ReadFindings.Response>(new ReadFindings.Query(request.Id, ArtifactKind.Verify), cancellationToken)
                .ConfigureAwait(false);

            if (standing.IsFailed)
            {
                return standing.ToResult<string>();
            }

            if (System.IO.File.Exists(current) && standing.Value.Artifacts.Any(a => a.Standing.Any(f => f.IsOpenBlocker)))
            {
                string verdict = (await System.IO.File.ReadAllTextAsync(current, cancellationToken).ConfigureAwait(false)).ReplaceLineEndings("\n").TrimEnd();
                previous = "\nThis build has a verdict already, and its interrogation raised the findings below. Answer each from\nthe evidence: standing by a claim and citing why is an answer, as is changing it.\n\n<file name=\"verify.md\">\n"
                    + verdict
                    + "\n</file>\n\n<findings>\n"
                    + FindingsRenderer.Render(standing.Value).ReplaceLineEndings("\n").TrimEnd()
                    + "\n</findings>\n";
            }

            return Result.Ok(shared + Template("adjudicate")
                .Replace("{{constraints}}", rules.Value, StringComparison.Ordinal)
                .Replace("{{perspectives}}", perspectives.ToString().TrimEnd(), StringComparison.Ordinal)
                .Replace("{{previous}}", previous, StringComparison.Ordinal)
                .Replace("{{id}}", cycle.Id.ToString(), StringComparison.Ordinal));
        }
    }
}
