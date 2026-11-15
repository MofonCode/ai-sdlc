using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;

namespace Sdlc.Core.Findings;

/// <summary>
/// Starts every reader whose prompt <c>sdlc round prompt</c> wrote, and keeps each answer beside its prompt (D-183).
/// </summary>
/// <remarks>
/// <para><strong>The launch the skills used to type, moved into the binary.</strong> Each reader is a fresh process
/// started from the round's own directory, outside the checkout, so the host hands it nothing it knows about the
/// repository (D-163); with no tools, no MCP servers and no session file, because everything it may read is in its
/// prompt (D-118); the prompt piped in, never passed as an argument; and marked as a reader, so a red build's turn-end
/// hook lets its turn end with its answer (D-109). The verify adjudicator is started the same way from its own
/// directory.</para>
/// <para><strong>The rules reader goes first, alone, and the rest after a short wait</strong> (D-163). The block every
/// reader but the change's shares is handed over as the system prompt, which is the point a host caches up to; started
/// together, no reader finds another's copy. The change's reader and the adjudicator are handed no shared block, as
/// before.</para>
/// <para><strong>What it decides is only who starts what.</strong> It judges nothing and writes no round:
/// <c>sdlc round collect</c> reads the answers as it always has.</para>
/// </remarks>
public static class RunRound
{
    /// <summary>The block the rules reader and every perspective open with, as <c>sdlc round prompt</c> writes it.</summary>
    public const string SharedFileName = "shared.md";

    /// <summary>What a prompt file ends with; the reader is named by what comes before it.</summary>
    public const string PromptSuffix = ".prompt.md";

    /// <summary>The reader started first, alone, so the shared block is cached before the rest arrive.</summary>
    public const string FirstReader = "rules";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRunRoundRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Starts a round's readers.</summary>
    /// <param name="Directory">Where <c>sdlc round prompt</c> wrote the prompts; each answer is written beside its prompt.</param>
    /// <param name="Warm">How long the first reader runs alone before the rest start.</param>
    /// <param name="Timeout">How long any one reader may take before it is stopped.</param>
    /// <param name="Text">
    /// Whether each answer is plain text, written as <c>&lt;name&gt;.md</c>, rather than a round reader's JSON, written as
    /// <c>&lt;name&gt;.json</c>. Mining's readers answer in prose.
    /// </param>
    public sealed record Command(string Directory, TimeSpan Warm, TimeSpan Timeout, bool Text = false) : IRequest<Response>;

    /// <summary>How one reader went.</summary>
    /// <param name="Name">The reader: <c>rules</c>, <c>diff</c>, <c>adjudicate</c>, or a perspective.</param>
    /// <param name="Answer">Where its answer was written.</param>
    /// <param name="Problem">What went wrong, or null when it answered.</param>
    /// <param name="Kept">Whether its answer was already there, so it was not started again.</param>
    /// <param name="NoHost">Whether it could not be started at all, because there is no host to start it with.</param>
    public sealed record Reader(string Name, string Answer, string? Problem, bool Kept = false, bool NoHost = false)
    {
        /// <summary>Whether it answered.</summary>
        public bool Answered => Problem is null;
    }

    /// <summary>Every reader the round started.</summary>
    /// <param name="Directory">The round's directory.</param>
    /// <param name="Readers">Each reader, in the order they were started.</param>
    public sealed record Response(string Directory, IReadOnlyList<Reader> Readers)
    {
        /// <summary>Whether every reader answered.</summary>
        public bool AllAnswered => Readers.All(reader => reader.Answered);
    }

    /// <summary>The arguments one reader is started with, after the word <c>claude</c>.</summary>
    /// <param name="name">The reader.</param>
    /// <param name="shared">Whether the round's directory holds a shared block.</param>
    /// <returns>The arguments, one each.</returns>
    /// <param name="text">Whether the answer is plain text rather than JSON.</param>
    public static IReadOnlyList<string> ArgumentsFor(string name, bool shared, bool text = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        List<string> arguments = ["-p", "--output-format", text ? "text" : "json", "--tools=", "--strict-mcp-config", "--no-session-persistence"];

        // The change's reader and the adjudicator read what no other reader shares, so neither is handed the block.
        if (shared && name is not ("diff" or "adjudicate"))
        {
            arguments.Add("--append-system-prompt-file");
            arguments.Add(SharedFileName);
        }

        return arguments;
    }

    /// <summary>What a person reads once the readers are done.</summary>
    /// <param name="response">How the round went.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();

        foreach (Reader reader in response.Readers)
        {
            if (reader.Kept)
            {
                text.Append(CultureInfo.InvariantCulture, $"  kept      {reader.Name,-16} {reader.Answer}\n");
            }
            else if (reader.Answered)
            {
                text.Append(CultureInfo.InvariantCulture, $"  answered  {reader.Name,-16} {reader.Answer}\n");
            }
            else
            {
                text.Append(CultureInfo.InvariantCulture, $"  FAILED    {reader.Name,-16} {reader.Problem}\n");
            }
        }

        text.Append(response.AllAnswered
            ? "Every reader answered.\n"
            : "A reader returned nothing. Run it again: only the readers with no answer yet are started.\n");

        return text.ToString();
    }

    private sealed class Handler(IAgentProcess agents, TimeProvider time) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!System.IO.Directory.Exists(request.Directory))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no directory at {request.Directory}. Write the prompts first: 'sdlc round prompt <id> <artifact> --dir {request.Directory}'."));
            }

            string[] names =
            [
                .. System.IO.Directory.GetFiles(request.Directory, "*" + PromptSuffix)
                    .Select(path => Path.GetFileName(path)[..^PromptSuffix.Length])
                    .Order(StringComparer.Ordinal),
            ];

            if (names.Length == 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{request.Directory} holds no prompt. Write them first: 'sdlc round prompt <id> <artifact> --dir {request.Directory}'."));
            }

            bool shared = File.Exists(Path.Combine(request.Directory, SharedFileName));

            // A reader with an answer already is not started again: the reconciler is started by a second run once its
            // prompt is written, and running again is how a reader that returned nothing is retried.
            Reader[] kept = [.. names.Where(name => Answered(AnswerOf(request, name))).Select(name => new Reader(name, AnswerOf(request, name), null, Kept: true))];
            string[] waiting = [.. names.Where(name => !kept.Any(reader => reader.Name == name))];
            string? first = waiting.Contains(FirstReader, StringComparer.Ordinal) && waiting.Length > 1 ? FirstReader : null;

            Task<Reader>? warming = first is null ? null : Start(first, request, shared, cancellationToken);

            if (warming is not null)
            {
                await Task.Delay(request.Warm, time, cancellationToken).ConfigureAwait(false);
            }

            Task<Reader>[] rest = [.. waiting.Where(name => name != first).Select(name => Start(name, request, shared, cancellationToken))];
            Reader[] done = await Task.WhenAll(warming is null ? rest : [warming, .. rest]).ConfigureAwait(false);

            if (done.FirstOrDefault(reader => reader.NoHost) is { } stranded)
            {
                return Result.Fail<Response>("No host to start a reader with: " + stranded.Problem);
            }

            return Result.Ok(new Response(request.Directory, [.. kept, .. done]));
        }

        private static string AnswerOf(Command request, string name) =>
            Path.Combine(request.Directory, name + (request.Text ? ".md" : ".json"));

        private static bool Answered(string answer) => File.Exists(answer) && new FileInfo(answer).Length > 0;

        private async Task<Reader> Start(string name, Command request, bool shared, CancellationToken cancellationToken)
        {
            string answer = AnswerOf(request, name);
            AgentExit exit = await agents.Run(
                new AgentLaunch(
                    request.Directory,
                    ArgumentsFor(name, shared, request.Text),
                    Path.Combine(request.Directory, name + PromptSuffix),
                    answer,
                    Reader: true,
                    request.Timeout),
                cancellationToken).ConfigureAwait(false);

            if (!exit.Succeeded)
            {
                return new Reader(name, answer, exit.Problem ?? "It did not finish.", NoHost: exit.NeverStarted);
            }

            return Answered(answer)
                ? new Reader(name, answer, null)
                : new Reader(name, answer, "It returned nothing.");
        }
    }
}
