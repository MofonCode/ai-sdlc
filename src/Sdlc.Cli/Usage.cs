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
        "One of: approval <id>, abandon <id>, solution, techstack. "
        + "A constraint is proposed rather than drafted: 'sdlc propose constraints'.";

    /// <summary>
    /// Which proposals exist, for a message about one that does not.
    /// </summary>
    /// <remarks>
    /// One subject today and the verb is plural-ready on purpose: the record has more than rules to say
    /// something about. Only what is built is offered, for the same reason the drafters are.
    /// </remarks>
    public const string Proposals = "One of: constraints, decisions, theme.";

    /// <summary>The usage text, written to standard output for help and to standard error for a
    /// usage error.</summary>
    public const string Text = """
        sdlc - the gate for an ai-sdlc governed solution.

        Usage
            sdlc <command> [arguments] [--option value]

        Commands
            init                     Adopt the framework into this repository. Run by a person.
            new                      Open a cycle: allocate its id and create its directory. Refuses
                                     until a declared stack is in place -- 'draft techstack'.
            scaffold <id>            Write the solution skeleton -- a solution file, its projects, the
                                     architecture tests and a reference slice -- and the .NET build
                                     settings it compiles under. On a solution adopted into existing
                                     code, never the skeleton: only the build settings files that
                                     cycle's plan declares. Writes what is absent and leaves what is
                                     there. Refuses without an approved plan: adoption writes no
                                     code and no build settings.
            constraints              Render the constraint set in force.
            gate <id>                Say whether a cycle's build may run. Exits 1 if not.
            verify-chain <id> [--base <branch>]
                                     Re-establish the whole chain from the repository. Exits 1 if
                                     it does not hold. This is the pull-request check. --base is the
                                     target branch: a pull request changing only the cycle's
                                     planning documents then gets the admission check -- plan,
                                     approval, hashes, no verify.
            admission <id> --base <branch>
                                     Print true when a pull request carries only the cycle's
                                     planning documents, so a pipeline may skip the build and
                                     tests; false for anything else. Always exits 0.
            branch <id> <kind>       Print the one git command that puts the working copy on the
                                     branch a kind of work happens on -- plan, build,
                                     build-unattended or forward -- creating it from the right
                                     base: the integration branch once the plan is admitted to
                                     it, the plan's own branch before that.
            factory                  Write the factory's pipeline for the recorded platform: every
                                     night, batch-build --pr --unattended against the integration
                                     branch, one run at a time, six hours at most, proving the
                                     gate refuses before it builds, and publishing the sitrep. It
                                     never merges. Refuses on a local solution.
            sitrep                   The morning's page, in Markdown: built cycles ready to merge
                                     and in what order, with the ones expected to conflict;
                                     what needs a person; what was held back and why; what is
                                     waiting and going stale. Fetch the remote first: a built
                                     cycle is read from its own branch.
            cycles --queued          What a build would take now -- approved, no verify, not
                                     abandoned -- and every cycle held back, with why: a
                                     cycle/<id>/... branch on the remote, or an approval the
                                     gate no longer accepts. Deleting the branch re-queues it.
            pr-ready <id>            Say whether a cycle may open a pull request, without opening
                                     one. Exits 1 if not. For every cycle, 'cycles --pr-ready'.
            draft approval <id>      Write the gate decision beside the ledger, for a person to
                                     rename onto it. Writes no approval.
            draft abandon <id>       Print the record that stops a cycle, for a person to place.
            draft techstack          Write the declared stack beside itself, for an edit a person
                                     renames into place: a copy of it, or the framework's default
                                     where there is none yet, with every stack rule listed and on.
                                     The default also lists the five .NET build settings files, one
                                     row each: on where the repository held no code, off where it
                                     did. Also reports the options menu: every concern the shipped
                                     profiles know about, what each declares, every stack rule, and
                                     what each build settings file does and costs. The menu is
                                     never renamed into place.
            draft solution           Write the whole solution.md beside itself, for a person
                                     to rename onto it. Carries the four keys through as
                                     recorded and the prose through unchanged, so the rename
                                     cannot reclassify the solution or cost anybody what they
                                     wrote. --standard changes that one key, and says so.
            evidence <id>            Record what a build produced: the diff, its base commit, and a
                                     normalised test summary. Verify reads only this.
            cycles                   List every cycle this solution holds, newest first: what each
                                     is about, where it stands, and what it is owed.
            status <id>              Show the panel for a stage. Ends with the next command.
            findings <id>            What the interrogations said, per artifact, each finding at the
                                     disposition that stands.
            suggestions [<id>]       List the open non-blocking findings and what the builds
                                     noticed, for one cycle or all.
            decisions                The choices this solution has already settled, so a cycle reads
                                     what was argued rather than arguing it again. CON_19 asks a plan
                                     departing from one to say what it would have cost.
            theme                  The roles this solution's surfaces use and the values that
                                     fill them -- the one place a colour or a typeface lives.
            stack                    What this solution declares it is built from: its defaults,
                                     the stack rules switched on, and each build settings file --
                                     whether the stack takes it and whether it is on disk. Names
                                     the gap and closes none: a cycle's plan does that.
            propose constraints      Read this solution's whole history and say which rules its
                                     findings keep citing, with the cycles and finding ids behind
                                     each. Proposes; writes nothing that binds. Where the record
                                     holds no pattern it prints an empty entry at the next free
                                     LOC_ id with the three admission tests beside it.
            propose decisions        Read what this solution's approved cycles stated -- an intent's
                                     fixed decisions, a plan's shape and departures -- and hand the
                                     passages over for a person to record. Approved cycles only:
                                     an approval is what makes a passage something a person signed.
            propose theme          Print the roles a theme declares, for a solution that has
                                     none, or name the ones left empty in the one it has. Never
                                     proposes a value: a colour is a brand's, not a tool's.
            doctor                   Check this installation and this repository: the rules, git,
                                     adoption, every host's hook config, and the pipeline that is
                                     the guarantee. Reports and never repairs. Exits 1 if anything
                                     is wrong. What a binary cannot see -- whether a host loaded
                                     the skills, whether the pipeline's check is required -- is
                                     marked 'elsewhere' and names who to ask. Before the first
                                     cycle it ends with what a person still has to place, and the
                                     one command that is next.
            hook --host <name>       Judge a tool call, or a turn that is ending. Reads the host's
                                     payload on stdin and writes its decision on stdout.
                                     Hosts: claude, copilot, antigravity.
            version                  Which version of the gate this is, and which versions it
                                     expects beside it.
            help                     This text.

        Common options
            --solution <path>        The solution to work in. Defaults to the working directory.

        Options for draft techstack
            --empty                  Write the draft with nothing in it. Renamed into place, it
                                     declares that nothing is pinned: no default for a plan to depart
                                     from, no stack rule in force, every plan choosing freely.

        Options for draft solution
            --standard <name>        Change which conventions new work follows: profile, or existing.
                                     The one key a draft may move, and only when asked. profile makes
                                     the framework's patterns the default a plan departs from;
                                     existing makes it the surrounding code. Neither says anything
                                     about the code already there, and neither writes a file -- the
                                     .NET build settings are rows in the declared stack.

        Options for init
            --platform <name>        Where the code lives: github, azure-devops, or local for a
                                     repository with no remote yet. Default: github.

                                     local writes no pipeline, so no pull-request check governs a
                                     merge and nothing mechanical decides what reaches the default
                                     branch. The choice is recorded in solution.md so doctor
                                     reports it as a choice rather than a broken install. Re-run
                                     init with a platform once there is a remote; the pr skill
                                     walks through creating one.

                                     On a repository already governed, re-running writes the
                                     pipeline and prints the solution.md keys for a person to
                                     place, because that file is protected. Until they are placed
                                     the record still says the old platform, and doctor and the pr
                                     skill both read the record.

                                     Omitting the option is not the same as passing github: on a
                                     governed repository it means keep what is recorded, so a
                                     re-run to pick up a new template never proposes moving a
                                     local solution onto a platform nobody asked for.

            Any other option is refused before anything is written, rather than
            ignored.

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
            --pr-ready               Every cycle, saying which may open one and why not.
            --claims                 The claim graph: which cycles may be built together, in groups,
                                     and what collides.

        Newest first, always. The order a person scans is the order the work happened in, and a
        second order would be a second way to read one answer.

        Options for decisions
            --brief                  Ids and statements only, without the clauses.
            --all                    Include the reversed entries, which stay on the page so a cycle
                                     that cited one is still readable. Without it, what stands.

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

        Two verbs, split by who made the decision
            'draft' is for a decision you have already taken: it hands back a finished file and the
            rename or the paste that places it. 'propose' is for one you have not: it reads the
            record and argues. A tool may propose a rule when it brings the evidence, and may not
            shape a rule somebody just typed -- which is why there is no way to pass wording in.

            'suggestions' is neither. It lists what a reader already raised and nobody acted on,
            and forms no opinion of its own.

        Three records, and what each stops
            'constraints' stops a cycle doing something. 'decisions' stops it re-deciding
            something. 'theme' stops it inventing a colour. All three are rendered on demand and
            never copied into an artifact, because a copy is a second place the truth lives.

            'stack' is read beside them and is not a fourth: it prints the declared stack, which
            CON_15 already judges a plan against, and adds the one comparison a file listing can
            decide -- which build settings the stack takes against which are on disk.

            A plan may depart from any of the three. Departing from a rule is impossible, from a
            recorded decision it is legal but CON_19 asks what the decision would have cost, and
            from the theme nothing in governance objects at all -- that one is a linter's job,
            because it is mechanical. Silence is what the rules catch, never the departure.

        Reports
            'suggestions' and 'propose' both print their answer and keep a copy under
            .sdlc/reports/, because a person works through one of these rather than reading it
            once. Nothing reads one back, ever: the directory ignores itself, and a report found
            later is already out of date if a cycle has run since. Re-run the command rather than
            trusting one.

        The stages
            Writing an intent, a spec or a plan, judging one against the rules, building, verifying
            and opening the pull request are skills rather than commands: they need language, so your
            agent runs them and this binary checks what they produced. Typing one here says so, and
            names the invocation. 'sdlc help' lists what the binary does on its own.

        Exit codes
            0  the command did what was asked
            1  a rule refused it
            2  the command line could not be understood
            3  the gate itself failed

        """;
}
