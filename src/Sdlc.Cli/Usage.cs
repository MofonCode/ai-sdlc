namespace Sdlc.Cli;

/// <summary>What the gate can be asked to do.</summary>
internal static class Usage
{
    /// <summary>The usage text, written to standard output for help and to standard error for a
    /// usage error.</summary>
    public const string Text = """
        sdlc - the gate for an ai-sdlc governed solution.

        Usage
            sdlc <command> [arguments] [--option value]

        Commands
            constraints              Render the constraint set in force.
            gate <id>                Say whether a cycle's build may run. Exits 1 if not.
            cycle-approval <id>      Print the approval entry for a person to paste.
            hook --host <name>       Judge a tool call. Reads the host's payload on stdin and
                                     writes its decision on stdout. Hosts: claude, copilot.
            help                     This text.

        Common options
            --solution <path>        The solution to work in. Defaults to the working directory.

        Options for constraints
            --for <artifact>         Narrow to one artifact: intent, spec, plan, diff, verify.
            --brief                  Ids and statements only, without the clauses.

        Options for cycle-approval
            --by <name>              Who is approving. Defaults to git's configured user name.

        Exit codes
            0  the command did what was asked
            1  a rule refused it
            2  the command line could not be understood
            3  the gate itself failed

        """;
}
