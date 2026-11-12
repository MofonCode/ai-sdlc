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
        "One of: approval <id>, abandon <id>, solution, techstack, mining <file>. "
        + "A constraint is proposed rather than drafted: 'sdlc propose constraints'.";

    /// <summary>
    /// Which proposals exist, for a message about one that does not.
    /// </summary>
    /// <remarks>
    /// One subject today and the verb is plural-ready on purpose: the record has more than rules to say
    /// something about. Only what is built is offered, for the same reason the drafters are.
    /// </remarks>
    public const string Proposals = "One of: constraints, decisions, theme. Reading every cycle at once is 'sdlc mine'.";

    /// <summary>The usage text, written to standard output for help and to standard error for a
    /// usage error.</summary>
    public const string Text = """
        sdlc - the gate for an ai-sdlc governed solution.

        Usage
            sdlc <command> [arguments] [--option value]

        Commands
            init                     Adopt the framework into this repository. Run by a person.
                                     Commits what it writes, creates the integration branch and
                                     switches to it, and drafts the three files only a person
                                     places -- solution, techstack, constraints -- beside them.
            new                      Open a cycle: allocate its id, claim it where every worktree of
                                     the repository looks, and create its directory. Refuses until
                                     a declared stack is in place. In a sandbox, the cycle opens
                                     beside the others. The intent skill runs it; you rarely type it.
            scaffold <id>            Write the solution skeleton -- a solution file, its projects, the
                                     architecture tests and a reference slice -- and the .NET build
                                     settings it compiles under. On a solution adopted into existing
                                     code, never the skeleton: only the build settings files that
                                     cycle's plan declares. Writes what is absent and leaves what is
                                     there. Refuses without an approved plan: adoption writes no
                                     code and no build settings.
            constraints              Render the constraint set in force.
            constraints show <id>    One rule in full, in force or superseded.
            areas                    Each area areas.md defines: the files it covers today, the
                                     rules and decisions carrying it, and entries naming an area
                                     nobody defined. Exits 1 when an area covers no file, which
                                     stops a round until it is fixed.
            gate <id>                Say whether a cycle's build may run. Exits 1 if not.
            verify-chain <id> [--base <branch>]
                                     Re-establish the whole chain from the repository. Exits 1 if
                                     it does not hold. This is the pull-request check. --base is the
                                     target branch: a pull request changing only the cycle's
                                     planning documents then gets the admission check -- plan,
                                     approval, hashes, no verify.
            verify-chain slices/<slug>/plan --base <branch>
                                     The same check for a slicing's pull request: every cycle it
                                     carries gets the admission check, its successors must be
                                     sound, and a plan that needs a file only a sibling's plan
                                     creates is refused (D-125).
            verify-chain <branch> --base <branch>
                                     On a branch that is no cycle's: no cycle needed when everything
                                     it changes is the pin, what 'sdlc init' and 'sdlc factory'
                                     write, answers in the declared stack to what the release asks,
                                     and dependency versions. Each file must be what this release
                                     writes from the base's copy, a pipeline step by step (D-133);
                                     a dependency file may move only versions, of any size (D-134).
                                     Anything else is refused: there is no chain.
            admission <id> --base <branch>
                                     Print true when a pull request carries only the cycle's
                                     planning documents, and any successors, so a pipeline may
                                     skip the build and tests; false for anything else. On
                                     slices/<slug>/plan, true when it carries only cycles'
                                     planning and successors. Always exits 0.
            promotion <source> --base <target>
                                     Print true when a pull request promotes the integration
                                     branch or a release/* branch into another branch, so a
                                     pipeline skips the chain; false when it targets the
                                     integration branch or a release branch. Exits 1 when any
                                     other branch targets another branch.
            branch-policy            Print the commands that set the default branch policy on
                                     this solution's platform: pull requests only, nobody
                                     bypassing, the pipeline required, squash into the
                                     integration branch and merge commits into the default
                                     branch. Applies nothing; run them as an administrator.
                                     --approvals <n> asks every pull request for n approvals;
                                     --owner <who>[,<who>] names who must give one, as a
                                     CODEOWNERS line on GitHub and a required reviewer on
                                     Azure DevOps; --self-approve lets an author's own vote
                                     count on Azure DevOps, and on GitHub, which never counts
                                     it, needs a team: --self-approve <team> lets that team
                                     bypass the rules on a pull request.
            branch <id> <kind>      Print the one git command that puts the working copy on the
                                     branch a kind of work happens on -- plan, build,
                                     build-unattended or forward -- creating it from the right
                                     base: the integration branch once the plan is admitted to
                                     it, the plan's own branch before that. 'sandbox' opens a
                                     worktree on sandbox/<id> beside the repository, where every
                                     cycle the work turns into is planned. 'plan --worktree'
                                     opens the one cycle in a folder of its own. 'plan' without a
                                     worktree refuses uncommitted work, which the switch would
                                     carry, and a local plan branch whose ledger lacks the
                                     approval the integration branch holds (D-123).
            factory                  Write the factory's pipeline for the recorded platform: a run
                                     a person starts, never a schedule, of batch-build --pr
                                     --unattended against the integration branch -- every queued
                                     cycle, or the one named as its input -- one run at a time,
                                     six hours at most, proving the hooks refuse before it builds,
                                     and publishing the sitrep. It never merges. Refuses on a
                                     local solution.
            lead show <reference>    Fetch a lead and print it in one shape for 'intent --from':
                                     azuredevops:1234 or github:56 through az or gh, a
                                     suggestion:<cycle>/<finding>, a ready successor:<slug>, or a
                                     file path. What came from outside is fenced as untrusted text.
                                     Refuses a successor that is not ready. Jira is not read yet.
            successors               Every slice waiting on another's code, as a file in
                                     .sdlc/successors/, and where each stands -- waiting, ready,
                                     stuck behind an abandoned cycle, or unresolved -- worked out
                                     from the record. Ready means everything it succeeds is on
                                     the integration branch with a passing verify (D-125).
            backlog                  Every successor and every parked cycle, in the waves they come
                                     ready in -- ready now, then after one more merge, and so on
                                     -- each with how to start it, and apart from them the stuck
                                     and the unresolved. A view; it writes nothing (D-145).
            discard                  In a sandbox: what throwing it away would lose. Each cycle
                                     is empty, can go, is worth recording -- it fixed a decision,
                                     or its readers raised something -- or is recorded already.
                                     Prints the removal for a person to run once nothing worth
                                     recording is unrecorded, and exits 1 until then. Deletes
                                     nothing.
            park <id>                Compare a cycle's plan with every plan not yet built. A file
                                     only another creates parks it: writes parked.md with what it
                                     waits on, keeping its spec and plan. A file two plans both
                                     name is reported. The plan skill runs it.
            unpark <id>              Take a parked cycle back once everything it waits on merged
                                     with a passing verify; its spec and plan are then read again
                                     against what merged. Refuses one still waiting, saying why.
            successor check          Refuse a successor file that cannot be read, one naming a
                                     cycle or slug that does not exist, and a loop through
                                     succeeds:. Exits 0 when the set is sound.
            successor start <slug> <id>
                                     In a cycle opened for a ready successor, on its plan branch:
                                     refuse unless it is ready, then delete its file there. The
                                     intent then records lead: successor:<slug>. /sdlc:intent
                                     --from successor:<slug> runs it.
            slices <slug> <id> <id>...
                                     Print the one git command that composes slices/<slug>/plan:
                                     the integration branch with each sibling's plan branch merged
                                     in, so one pull request admits every plan sliced together.
                                     Refuses a sibling with no plan branch, plan or approval
                                     ledger, and a plan that needs a file only a sibling's plan
                                     creates; names the files two plans share. /sdlc:pr --plan
                                     runs it.
            admit                    In a sandbox: print the one git command that takes its
                                     approved cycles' folders, and the successors it wrote, onto
                                     cycle/<id>/plan for one or slices/sandbox-<id>/plan for
                                     several, from the integration branch, in a worktree beside
                                     the sandbox. What the gate does not let through stays. Refuses
                                     outside a sandbox, with nothing approved, over uncommitted
                                     changes in an approved cycle, and a plan that needs a file
                                     only a sibling's plan creates. Abandoned cycles go with it as
                                     records. /sdlc:pr --plan runs it.
            operate [--since <hours>h] [--dry-run]
                                     Read production's errors from the telemetry store solution.md
                                     declares, grouped by signature, personal data stripped, and
                                     file each new one as a bug on the platform's tracker -- a lead.
                                     Not filed again while open, or once a person dismissed it.
                                     A person runs it; no pipeline does.
            perspective show <name> --for <stage> [--cover]
                                     One perspective for one stage -- intent, spec, plan, build or
                                     verify: its method, the questions a fresh reader asks, its
                                     rules and its evidence; with --cover, the author's short list.
            perspectives <id>        Which perspectives a cycle gets and why: always on, named by
                                     the intent, or brought by the plan's declared files.
            sitrep                   The morning's page, in Markdown: built cycles ready to merge
                                     and in what order, with the ones expected to conflict;
                                     what needs a person; what was held back and why; what is
                                     waiting and going stale; the backlog -- every successor and
                                     parked cycle, ready, waiting or stuck -- and what it cannot
                                     see. Fetch the remote first: a built cycle is read from its
                                     own branch. It starts nothing.
            cycles --here [--for <step>]
                                     Which cycle a skill given no id means: the cycles this
                                     branch carries -- its own on a cycle branch, otherwise
                                     those not yet on the integration branch -- that are ready
                                     for to-plan, spec, plan, build, verify, remediate or pr.
                                     Exactly one:
                                     prints its id alone and exits 0. Several, or none: lists
                                     them and exits 1, so the person is asked, never guessed for.
            cycles --queued [<id>]   What a build would take now -- approved, no verify, not
                                     abandoned -- in the order to build it: the cycle the most
                                     successors wait on first, then oldest. And every cycle held
                                     back, with why: a cycle/<id>/... branch on the remote, or an
                                     approval the gate no longer accepts. Deleting the branch
                                     re-queues it. With an id, that cycle alone, and exits 1 with
                                     why when it is not queued.
            pr-ready <id>            Say whether a cycle may open a pull request, without opening
                                     one. Exits 1 if not -- and always for a test run, a cycle
                                     a local build judged. For every cycle, 'cycles --pr-ready'.
            draft approval <id>      Write the gate decision beside the ledger, for a person to
                                     rename onto it. Writes no approval.
            draft abandon <id>       Print the record that stops a cycle, for a person to place.
            draft mining <file>      Write drafts of constraints.md, decisions.md and techstack.md
                                     from the proposals marked [x] in a mining proposal file:
                                     each a copy of the file as it stands with only those lines
                                     applied, refused if any entry or row would go missing.
            draft techstack          Write the declared stack beside itself, for an edit a person
                                     renames into place: a copy of it, or the framework's default
                                     where there is none yet, with every stack rule listed and on.
                                     The default also lists the five .NET build settings files, one
                                     row each: on where the repository held no code, off where it
                                     did. A row not true here says 'none' rather than being
                                     deleted. On a copy, names what this release ships that the
                                     stack never mentions, and adds none of it. Also reports the
                                     options menu: every concern the shipped profiles know about,
                                     what each declares, every stack rule, and what each build
                                     settings file does and costs. The menu is never renamed into
                                     place.
            draft solution           Write the whole solution.md beside itself, for a person
                                     to rename onto it. Carries the four keys through as
                                     recorded and the prose through unchanged, so the rename
                                     cannot reclassify the solution or cost anybody what they
                                     wrote. --standard changes that one key, and says so.
            evidence <id>            Record what a build produced: its base commit, the commit it is
                                     at, the diff between them, a normalised test summary and the
                                     coverage. Verify reads only this. The summary records the
                                     platform the tests ran on. Refuses code outside .sdlc/ that is
                                     not committed; the diff is rebuilt from the two commits and
                                     never committed (D-124).
            evidence check <id>      List what verify.md cites in backticks that none of the
                                     recorded evidence holds, and tests it says passed that did not.
                                     Reports only; always exits 0.
            cycles                   List every cycle this solution holds, newest first: what each
                                     is about, where it stands, and what it is owed.
            status <id>              Show the panel for a stage. Names beside the gate any recorded
                                     decision the cycle departs from, and ends with the next
                                     command and any successor that is ready or stuck.
            findings <id>            What the interrogations said, per artifact, each finding at the
                                     disposition that stands.
            deviations <id>          Where the build bent the approved documents: each deviation
                                     in deviations.md, whether a person accepted, declined or has
                                     yet to answer it, and anything wrong with it. Exits 1 while a
                                     deviation misquotes the approved text or takes a claimed file.
            round <id> <artifact>    Append the next round to the artifact's findings ledger from
                                     --from <file>: the readers' findings merged in, stamped with
                                     the time and the hashes of what was judged, and chained to the
                                     round before; suggestions go to the cycle's suggestions ledger,
                                     each concern once under an S_ id. Refuses while anything above
                                     is unsettled, a known finding restated in other words, a new
                                     blocker with no one-line gist, and a suggestion whose id or move
                                     does not fit the cycle's. --changed lists the sections moved
                                     since the last round instead.
            round prompt <id> <artifact> --dir <directory>
                                     Write every reader's prompt for a round into a directory
                                     outside the repository, as <reader>.prompt.md: the block every
                                     reader shares first, then each reader's own part. --reader
                                     <name> writes one -- rules, diff on a verify, a perspective,
                                     reconcile once the others have answered, or adjudicate on a
                                     verify. --list names the readers and writes nothing.
            round collect <id> <artifact> --dir <directory>
                                     Read every reader's answer from that directory and draft
                                     round.json beside them: new ids numbered past the ledgers,
                                     the reconciler's restatements carried under the standing id,
                                     each reader's usage carried for 'round --from' to record.
                                     Lists the open findings no reader raised, whose dispositions
                                     are yours to add. --host names the host the readers ran on.
            round verdict <id> --from <file>
                                     Write verify.md from the adjudicator's answer, byte for byte.
                                     Refuses an answer that is not a verify document.
            suggestions [<id>]       List the open non-blocking findings and what the builds
                                     noticed, for one cycle or all, each concern once. --all lists
                                     every status -- accepted, declined, answered, withdrawn --
                                     with the cycle that decided it.
            decisions                The choices this solution has already settled, so a cycle reads
                                     what was argued rather than arguing it again. CON_24 asks a cycle
                                     departing from one to declare it where it arises. Names
                                     the entries over the word budget, for mining to reword.
            decisions show <id>      One decision in full, standing or superseded: what a reader
                                     handed only its headline asks for.
            theme                 The roles this solution's surfaces use and the values that
                                     fill them -- the one place a colour or a typeface lives.
            stack                    What this solution declares it is built from: its defaults,
                                     the stack rules switched on, and each build settings file --
                                     whether the stack takes it and whether it is on disk. Then
                                     what this release ships that the stack never mentions: a row
                                     saying 'none' or false is an answer, no row is never asked.
                                     Names the gap and closes none: a cycle's plan does that.
            settings                 How this solution is set to run: each key in solution.md,
                                     declared or defaulted, and how to change it; what this
                                     machine overrides; what this release fixes, and the decision
                                     that fixes it. Points at constraints, stack and decisions
                                     rather than repeating them. Writes nothing. Exits 1 when
                                     solution.md cannot be read.
            mine                     Everything the record says the rules, decisions and stack are
                                     missing, in one ranked report: rules cited across cycles,
                                     findings that rested on no rule grouped by who raised them,
                                     each cycle's rounds, reopens and every verdict its verify
                                     reached, what approved cycles stated, the open suggestions
                                     of the finished cycles and the concerns ignored before, and
                                     what the stack never mentions.
                                     Ends with the proposal file a person marks for 'draft
                                     mining'. Proposes; writes nothing that binds.
            mine record <file>       Once the drafts are renamed into place: record the sitting in
                                     .sdlc/mined.json -- each finished cycle on the integration
                                     branch becomes a husk, with its merge commit, its files, the
                                     rules it cited and the areas it proposed, and the concerns
                                     under '## Ignored' tallied -- and print the
                                     deletion of the husks' folders, for a person to run on a
                                     branch of its own. Deletes nothing. Refuses while a draft is
                                     still beside its file.
            mine --history           The mining ledger: every mined cycle, every sitting with what
                                     was accepted and declined, the concerns watched below the
                                     bar of three cycles, and the concerns ignored, with the
                                     cycles each was ignored in.
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
                                     how many decisions every reader is handed,
                                     adoption, which release wrote adoption's files against this
                                     binary, line endings, every host's hook config, and the
                                     pipeline that is the guarantee, and asks the platform
                                     through az or gh whether branch policy requires it.
                                     Reports and never repairs. Exits 1 if anything is wrong, a
                                     policy that does not require the check included. What it
                                     cannot see -- whether a host loaded the skills, or the
                                     policy when the CLI is not signed in -- is marked
                                     'elsewhere' and names who to ask. Before the first
                                     cycle it ends with what a person still has to place, and the
                                     one command that is next.
            hook --host <name>       Judge a tool call, or a turn that is ending, or say as a session
                                     starts that its skills are another release. Reads the host's
                                     payload on stdin and writes its decision on stdout.
                                     Hosts: claude, copilot, antigravity.
            version                  Which version of the gate this is, and which versions it
                                     expects beside it. Inside a solution that pins another
                                     release, every command -- this one too -- is answered by
                                     the pinned release. With SDLC_DEV_BUILD set, by that build,
                                     which calls itself <release>-local.
            claude [args]            Start Claude Code with the skills this release carries, which
                                     in a solution that pins a release are that release's. Every
                                     argument after 'claude' is Claude Code's, passed unchanged.
            help                    This text. So does --help after any command.

        Common options
            --solution <path>        The solution to work in. Defaults to the working directory.

        Environment
            SDLC_DEV_BUILD           A directory holding a build of sdlc nobody published. Every
                                     command is handed to it instead of the pinned release, it
                                     calls itself <release>-local, and no cycle it judged opens a
                                     pull request. For testing a change to the framework.

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

            --integration-branch <name>
                                     Where approved plans wait and built cycles land. Default:
                                     development. Drafted into solution.md, and created from
                                     adoption's commit when it does not exist yet. Refused when
                                     solution.md already records a different one: only a person
                                     changes that file.

            Any other option is refused before anything is written, rather than
            ignored.

        Options for hook
            --event <name>           Which moment: pre-tool-use, turn-end or session-start.
                                     Defaults to pre-tool-use. Each host's own name for a moment is
                                     accepted too, so a config copied from its documentation works.
                                     A host with no turn-end event is refused by name rather than
                                     given a check that cannot run.
            --plugin <path>          With session-start: the plugin's directory, whose release is
                                     compared with this binary's. Never refuses.

        Options for constraints
            --for <artifact>         Narrow to one artifact: intent, spec, plan, diff, verify.
            --brief                  Ids and statements only, without the clauses.
            --cycle <id>             With --for: this solution's own rules outside the areas that
                                     stage of the cycle touches are shown by their headline. From
                                     the plan on; the intent and spec get every rule in full.
                                     'constraints show <id>' prints one rule in full.

        Options for evidence
            --tests <path>           Where the run's results are: one results file, or the directory
                                     holding every one it wrote. Point it at the directory -- each
                                     test project writes its own, and a summary built from one of
                                     them is a verification reading part of the suite.
            --build-log <path>       A build log to keep alongside.
            --coverage <path>        The run's cobertura report, or the directory holding every one
                                     it wrote. Measures the changed lines and the whole codebase,
                                     against the baseline at the base commit and the solution's
                                     coverage_floor, and carries the rate forward when it holds.


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
            --for <stage> --cycle <id>
                                     Every decision's headline, and in full only the ones with no
                                     area or in an area that stage touches: none for the intent and
                                     spec, the plan's declared files from the plan, the change's
                                     files on a verify. Nothing narrows without an areas.md.
            --area <name>            Only the decisions carrying one area.

        Options for findings
            --stage <artifact>       One document: intent, spec, plan or verify. Defaults to all four.
            --round <n>              One round. Defaults to every round. A finding is still shown at
                                     the disposition the latest round gave it, so asking about round
                                     one never reports an answered blocker as open.
            --brief                  One line per finding, without the statement or the remedy.
            --blocking               Only the open blocking findings: what stops the cycle now.
            --finding <id>           One finding, such as F_13 or security.F_04. With --stage, one
                                     artifact's; without it, that id in every artifact that has one.

        Options for status
            --stage <artifact>       Which panel: intent, spec, plan or verify. Defaults to
                                     whichever artifact the cycle is waiting on, or the
                                     furthest one written once all three are clear.
            --brief                  Four lines: the cycle, its stage, the blocker and suggestion
                                     counts, the gate, what the artifact says, and the next
                                     command. The same next command as the full panel.

        Options for draft approval
            --by <name>              Who is deciding. Defaults to git's configured user name.
            --changes-requested      Draft a send-back rather than an approval.
            --deny                   Draft a refusal.
            --reopen                 Draft a withdrawal of a standing approval, which returns the
                                     cycle to unapproved so the documents can be rewritten. Once
                                     the plan is on the integration branch, refused on the cycle's
                                     build branch: reopen on its plan branch instead (D-123).

        A refusal is drafted on the same terms as an approval, and leaves the reason line empty for
        the person to write. The gate rejects a refusal that says nothing about why.

        On an approved cycle whose build recorded deviations, the same command drafts the answer to
        each one instead: accepted: false and an empty note per deviation, beside the approval that
        stands. Set accepted to true to accept one, or leave it false and write why to decline it.
        False with no note is not reviewed. A reopen is refused until every deviation is answered.

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
            recorded decision it is legal but CON_24 asks it to be declared where it arises, and
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
