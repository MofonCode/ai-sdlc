namespace Sdlc.Cli;

/// <summary>What the gate can be asked to do.</summary>
internal static class Usage
{
    /// <summary>
    /// Which drafters exist, for a message about one that does not.
    /// </summary>
    /// <remarks>
    /// Only what is built is offered. Naming a drafter this cannot run would send someone looking for
    /// the flag that makes it work, which is the confusion the whole draft pattern exists to avoid.
    /// </remarks>
    public const string Drafters =
        "One of: approval <id>, abandon <id>, constraint, profile <name>.";

    /// <summary>The usage text, written to standard output for help and to standard error for a
    /// usage error.</summary>
    public const string Text = """
        sdlc - the gate for an ai-sdlc governed solution.

        Usage
            sdlc <command> [arguments] [--option value]

        Commands
            constraints              Render the constraint set in force.
            gate <id>                Say whether a cycle's build may run. Exits 1 if not.
            verify-chain <id>        Re-establish the whole chain from the repository. Exits 1 if
                                     it does not hold. This is the pull-request check.
            draft approval <id>      Print the gate entry for a person to paste.
            draft abandon <id>       Print the record that stops a cycle, for a person to place.
            draft constraint         Print the next free LOC_ id, with its four parts empty.
            draft profile <name>     Print the solution.md keys for a profile migration.
            status <id>              Show the panel for a stage. Ends with the next command.
            suggestions [<id>]       List the open non-blocking findings, for one cycle or all.
            hook --host <name>       Judge a tool call. Reads the host's payload on stdin and
                                     writes its decision on stdout. Hosts: claude, copilot.
            help                     This text.

        Common options
            --solution <path>        The solution to work in. Defaults to the working directory.

        Options for constraints
            --for <artifact>         Narrow to one artifact: intent, spec, plan, diff, verify.
            --brief                  Ids and statements only, without the clauses.

        Options for status
            --stage <artifact>       Which panel: intent, spec or plan. Defaults to whichever
                                     artifact the cycle is waiting on.

        Options for draft approval
            --by <name>              Who is deciding. Defaults to git's configured user name.
            --changes-requested      Draft a send-back rather than an approval.
            --deny                   Draft a refusal.
            --reopen                 Draft a withdrawal of a standing approval, which returns the
                                     cycle to unapproved so the documents can be rewritten.

        A refusal is drafted on the same terms as an approval, and leaves the reason line empty for
        the person to write. The gate rejects a refusal that says nothing about why.

        Exit codes
            0  the command did what was asked
            1  a rule refused it
            2  the command line could not be understood
            3  the gate itself failed

        """;
}
