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
            init                     Adopt the framework into this repository. Run by a person.
            new                      Open a cycle: allocate its id and create its directory.
            scaffold <id>            Write the solution skeleton -- a solution file, its projects, the
                                     architecture tests and a reference slice. Refuses without an
                                     approved plan: adoption writes no code.
            constraints              Render the constraint set in force.
            gate <id>                Say whether a cycle's build may run. Exits 1 if not.
            verify-chain <id>        Re-establish the whole chain from the repository. Exits 1 if
                                     it does not hold. This is the pull-request check.
            landable <id>            Say whether a cycle may be landed, without landing it. Exits 1
                                     if not. For every cycle, 'cycles --landable'.
            draft approval <id>      Write the gate decision beside the ledger, for a person to
                                     rename onto it. Writes no approval.
            draft abandon <id>       Print the record that stops a cycle, for a person to place.
            draft constraint         Print the next free LOC_ id, with its four parts empty.
            draft profile <name>     Print the solution.md keys for a profile migration.
            evidence <id>            Record what a build produced: the diff, its base commit, and a
                                     normalised test summary. Verify reads only this.
            cycles                   List every cycle this solution holds, newest first: what each
                                     is about, where it stands, and what it is owed.
            status <id>              Show the panel for a stage. Ends with the next command.
            findings <id>            What the interrogations said, per artifact, each finding at the
                                     disposition that stands.
            suggestions [<id>]       List the open non-blocking findings, for one cycle or all.
            doctor                   Check this installation and this repository: the rules, git,
                                     adoption, every host's hook config, and the pipeline that is
                                     the guarantee. Reports and never repairs. Exits 1 if anything
                                     is wrong. What a binary cannot see -- whether a host loaded
                                     the skills, whether a git hook is installed in this clone --
                                     is marked 'elsewhere' and names who to ask.
            hook --host <name>       Judge a tool call, or a turn that is ending. Reads the host's
                                     payload on stdin and writes its decision on stdout.
                                     Hosts: claude, copilot, antigravity.
            version                  Which version of the gate this is, and which versions it
                                     expects beside it.
            help                     This text.

        Common options
            --solution <path>        The solution to work in. Defaults to the working directory.

        Options for init
            --profile <name>         Which profile governs the solution. Default: dotnet-default.
            --adopt-standard         Hold this solution's new work to the profile's conventions, in a
                                     repository that existed first. Writes the stack files an adopted
                                     solution otherwise never gets -- .editorconfig, the two props
                                     files, global.json -- and records standard: profile.

                                     A person's decision rather than a default, because it applies a
                                     convention to code that never agreed to one:
                                     Directory.Build.props makes warnings errors, so the next build
                                     can fail on files nobody has touched. Nothing is overwritten --
                                     a file the repository already had is kept -- and the code
                                     already there is not migrated by this. A cycle does that.

                                     Nothing on a scaffolded solution, which is on the profile's
                                     standard already. On a repository already governed, the stack
                                     files are written and the solution.md key is printed for a
                                     person to place, because that file is protected.

            --platform <name>        Where the code lives: github, azure-devops, or local for a
                                     repository with no remote yet. Default: github.

                                     local writes no pipeline, so no pull-request check governs a
                                     merge -- the pre-push hook is the only mechanical layer and it
                                     is bypassable. The choice is recorded in solution.md so doctor
                                     reports it as a choice rather than a broken install. Re-run
                                     init with a platform once there is a remote; the land skill
                                     walks through creating one.

        Options for hook
            --event <name>           Which moment: pre-tool-use or turn-end. Defaults to
                                     pre-tool-use. Each host's own name for a moment is accepted
                                     too, so a config copied from its documentation works. A host
                                     with no turn-end event is refused by name rather than given a
                                     check that cannot run.

        Options for constraints
            --for <artifact>         Narrow to one artifact: intent, spec, plan, diff, verify.
            --brief                  Ids and statements only, without the clauses.

        Options for evidence
            --tests <path>           Where the run's results are: one results file, or the directory
                                     holding every one it wrote. Point it at the directory -- each
                                     test project writes its own, and a summary built from one of
                                     them is a verification reading part of the suite.
            --build-log <path>       A build log to keep alongside.

        Options for cycles
            --stage <artifact>       Only cycles whose furthest document is this one.
            --approved               Only cycles a person has approved.
            --verified               Only cycles whose verification passed.
            --landable               Every cycle, saying which may be landed and why not.
            --claims                 The claim graph: which cycles may be built together, in groups,
                                     and what collides.

        Newest first, always. The order a person scans is the order the work happened in, and a
        second order would be a second way to read one answer.

        Options for findings
            --stage <artifact>       One document: intent, spec, plan or verify. Defaults to all four.
            --round <n>              One round. Defaults to every round. A finding is still shown at
                                     the disposition the latest round gave it, so asking about round
                                     one never reports an answered blocker as open.
            --brief                  One line per finding, without the statement or the remedy.

        Options for status
            --stage <artifact>       Which panel: intent, spec or plan. Defaults to whichever
                                     artifact the cycle is waiting on.
            --brief                  Four lines: the cycle, its stage, the blocker and suggestion
                                     counts, the gate, what the artifact says, and the next
                                     command. The same next command as the full panel.

        Options for draft approval
            --by <name>              Who is deciding. Defaults to git's configured user name.
            --changes-requested      Draft a send-back rather than an approval.
            --deny                   Draft a refusal.
            --reopen                 Draft a withdrawal of a standing approval, which returns the
                                     cycle to unapproved so the documents can be rewritten.

        A refusal is drafted on the same terms as an approval, and leaves the reason line empty for
        the person to write. The gate rejects a refusal that says nothing about why.

        The draft is a complete file named for its decision -- approval.approved.draft.md and so on
        -- written beside the ledger and never onto it. Renaming it is what records the decision,
        and no command does that: every agent write to the ledger is denied, and so is every agent
        command that would move anything onto it. A draft nobody renamed records nothing.

        Where a decision already stands, the draft reproduces it above the new entry, so the rename
        replaces the ledger without losing what it held.

        The stages
            Writing an intent, a spec or a plan, judging one against the rules, building, verifying
            and landing are skills rather than commands: they need language, so your agent runs
            them and this binary checks what they produced. Typing one here says so, and names the
            invocation. 'sdlc help' lists what the binary does on its own.

        Exit codes
            0  the command did what was asked
            1  a rule refused it
            2  the command line could not be understood
            3  the gate itself failed

        """;
}
