# Testing a local build

**Running a cycle end to end on a change to the framework that nobody has published.** You are changing a
skill, a check or a hook, and you want to watch a real cycle go through it before a preview exists. This guide
is for somebody working on ai-sdlc itself; nobody using a published release needs it.

**A test run stays a test run.** A build nobody published calls itself `<release>-local`, every round it writes
records that, and no cycle a local build judged opens a pull request, in any form (D-135). Nobody else can restore
the build, so nothing it decided can be checked again. Run the cycle, learn what you needed, abandon it, publish,
and do the work again on the preview.

## 1. Build what you want to test

From your clone of this repository, publish the binary to a folder of its own:

```pwsh
dotnet publish src\Sdlc.Cli\Sdlc.Cli.csproj -o C:\sdlc-dev
```

A folder of its own rather than the build output, because a running session holds `sdlc.dll` open and Windows
will not let a build overwrite it. Publish again whenever you change something, with no session open.

The folder carries the plugin beside the binary — the skills, the hooks and both manifests — so the session you
start in step 3 runs your skills too, not only your binary.

**To test a branch somebody else pushed**, take the package its pull request built. Every run of `ci.yml` keeps
the packed tool as an artifact named `nupkg`, for seven days:

```pwsh
gh run download <run id> --repo MofonCode/ai-sdlc --name nupkg --dir C:\sdlc-feed
```

```pwsh
dotnet tool install MofonCode.Sdlc.Cli --tool-path C:\sdlc-dev --add-source C:\sdlc-feed --version <the version in the package's file name>
```

Install into an empty folder: two builds in one folder are refused rather than guessed between.

## 2. Switch it on

```pwsh
$env:SDLC_DEV_BUILD = 'C:\sdlc-dev'
```

In this terminal only, and that is deliberate: **every** `sdlc` started from it now runs your build, in every
solution, whatever the solution pins. Set it for the whole machine and you will forget it is on. Check:

```pwsh
sdlc version
```

The first line ends in `-local`. If it does not, the variable is not set in this terminal.

## 3. Start the session

In the solution you are testing against:

```pwsh
sdlc claude
```

It starts Claude Code with your build's skills and hooks. The session-start hook says, to the agent and to
you, that a local build is running and from where. **It says so every session**, for as long as the variable is
set.

**Test in a sandbox solution when you can.** A governed solution that exists only for this — a scratch adoption,
or a clone of a real one whose remote is a bare repository on your disk — costs nothing to throw away, and you
can exercise branches and merges there without touching anybody's work. A real solution works too; the work is
done again later.

## 4. Run the cycle

Every stage runs as it normally does. The status panel says when a cycle's rounds came from more than one
release, so a cycle you started on a published release and continued on your build says that too.

**`/sdlc:pr` is where it stops.** `sdlc pr-ready` refuses a cycle any of whose rounds a local build wrote, and
so does `sdlc verify-chain`, which is the solution's pull-request check — so a pull request opened by hand is
refused as well. The refusal names each round. [Handling refusals](handling-refusals.md#a-test-run) has the
entry.

## 5. Finish the test run

```pwsh
sdlc draft abandon <id>
```

Place what it prints, as for any abandoned cycle. Then switch the build off:

```pwsh
Remove-Item Env:SDLC_DEV_BUILD
```

## 6. Ship it, and run the work for real

1. **Publish the change.** Move the patch in `Directory.Build.props` and both plugin manifests, and push your
   branch of ai-sdlc. `ci.yml` publishes `<release>-preview`.
2. **Pin the solution to it.** On the solution's integration branch:

   ```pwsh
   dotnet tool update --local MofonCode.Sdlc.Cli --version <release>-preview
   ```

   ```pwsh
   sdlc init
   ```

   Commit, and open a pull request. One that only moves the pin needs no cycle (D-133).
3. **Open the cycle again** on the preview, from its intent, and run it through to `/sdlc:pr`.

## Pinning a local build in a sandbox

A sandbox can go further and pin a local build, so its own pipeline and every terminal run it without the
variable. Pack it with a name of its own each time — the machine's package folder keeps the first package of
any version and never fetches it again:

```pwsh
dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -o C:\sdlc-feed -p:VersionSuffix=local.20261106-0930
```

A pack made outside a pipeline with no suffix given is stamped `-local` anyway, so a local pack is never
mistaken for a release. In the sandbox:

```pwsh
dotnet tool update --local MofonCode.Sdlc.Cli --version 0.36.7-local.20261106-0930 --add-source C:\sdlc-feed
```

**Never in a real solution.** The pin is a committed file: it merges with whatever carries it, and then nothing
but your machine can restore the solution's gate. The pull-request check refuses a solution that pins one.

## What this guide does not cover

Installing a published release, which is [Installing the plugin](installing-the-plugin.md). The design, and what
it rejected, is `CONCEPT.md` D-135.
