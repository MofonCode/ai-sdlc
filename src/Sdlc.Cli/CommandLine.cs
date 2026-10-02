using System.Diagnostics.CodeAnalysis;

namespace Sdlc.Cli;

/// <summary>
/// A parsed command line: a verb, its positional arguments, and its _options.
/// </summary>
/// <remarks>
/// Hand-rolled rather than taken from a command-line library, for two reasons that are specific to
/// this tool. A pre-tool hook invokes the gate on every tool call an agent makes, so start-up is on
/// the critical path of every edit and a parser graph built per run is a cost paid thousands of
/// times a day. And the hook verb reads a JSON payload from standard input and writes a JSON
/// decision, which no argument parser helps with. The surface is <c>sdlc &lt;verb&gt; [args]
/// [--option value]</c>; if it grows past what this reads clearly, that is the moment to revisit.
/// </remarks>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options;

    private CommandLine(string verb, IReadOnlyList<string> arguments, Dictionary<string, string?> options)
    {
        Verb = verb;
        Arguments = arguments;
        _options = options;
    }

    /// <summary>The verb, lower-cased. Empty when none was given.</summary>
    public string Verb { get; }

    /// <summary>The positional arguments after the verb, in order.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Parses an argument vector.
    /// </summary>
    /// <param name="args">The arguments, as received.</param>
    /// <returns>The parsed line.</returns>
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string verb = args.Count > 0 && !args[0].StartsWith('-')
            ? args[0].ToLowerInvariant()
            : string.Empty;

        List<string> arguments = [];
        Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

        for (int index = verb.Length > 0 ? 1 : 0; index < args.Count; index++)
        {
            string current = args[index];

            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                arguments.Add(current);
                continue;
            }

            string name = current[2..];
            int equals = name.IndexOf('=', StringComparison.Ordinal);

            if (equals >= 0)
            {
                _options[name[..equals]] = name[(equals + 1)..];
                continue;
            }

            // A following token that is not itself an option is this option's value; otherwise the
            // option is a flag. That keeps `--brief` and `--for intent` in one syntax.
            bool hasValue = index + 1 < args.Count
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal);

            _options[name] = hasValue ? args[++index] : null;
        }

        return new CommandLine(verb, arguments, _options);
    }

    /// <summary>
    /// The same line with the first positional argument removed.
    /// </summary>
    /// <remarks>
    /// For a verb that takes a sub-verb. Dropping the consumed word here means everything downstream
    /// reads argument zero as the thing it expects, rather than each handler knowing how deep it sits.
    /// </remarks>
    /// <returns>A line whose arguments start after the first.</returns>
    public CommandLine WithoutFirstArgument() =>
        new(Verb, Arguments.Count == 0 ? [] : [.. Arguments.Skip(1)], _options);

    /// <summary>Whether an option was given, with or without a value.</summary>
    /// <param name="name">The option name, without leading dashes.</param>
    /// <returns>True when it was given.</returns>
    public bool HasOption(string name) => _options.ContainsKey(name);

    /// <summary>Reads an option's value.</summary>
    /// <param name="name">The option name, without leading dashes.</param>
    /// <param name="value">The value, when the option was given with one.</param>
    /// <returns>True when the option was given with a value.</returns>
    public bool TryOption(string name, [NotNullWhen(true)] out string? value)
    {
        value = _options.TryGetValue(name, out string? found) ? found : null;
        return value is not null;
    }
}
