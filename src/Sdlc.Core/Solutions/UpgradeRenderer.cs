using System.Globalization;
using System.Text;
using Sdlc.Core.Init;

namespace Sdlc.Core.Solutions;

/// <summary>Renders what <c>sdlc upgrade</c> found as one ordered checklist (D-170).</summary>
public static class UpgradeRenderer
{
    private const string Indent = "     ";

    /// <summary>Renders the checklist.</summary>
    /// <param name="upgrade">What moves, and from where.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(UpgradeChecklist.Response upgrade)
    {
        ArgumentNullException.ThrowIfNull(upgrade);

        StringBuilder output = new();
        int step = 0;

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Moving to sdlc {upgrade.Target}{(upgrade.Pinned is { } pinned ? " from " + pinned : string.Empty)}. This runs nothing; each step is yours, in this order."));
        output.AppendLine();

        // The release being moved to knows what it changed; an older one answering would describe the wrong upgrade.
        if (upgrade.MachineFirst)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. This machine runs {upgrade.Machine}. Move it first, then ask again: {upgrade.Target} answers the rest."));
            Commands(output, $"dotnet tool update --global {PinnedRelease.PackageId} --version {upgrade.Target}", $"sdlc upgrade {upgrade.Target}");
            return output.ToString();
        }

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. This machine runs {upgrade.Machine}. Nothing to do."));
        output.AppendLine();

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. The installed plugin, which plain 'claude' loads. 'sdlc claude' loads the pinned release's own copy, so this one is the fallback. On Claude Code:"));
        Commands(output, "claude plugin marketplace update ai-sdlc", "claude plugin uninstall sdlc@ai-sdlc", "claude plugin install sdlc@ai-sdlc");
        output.AppendLine(Indent + "Another host: docs/guides/installing-the-plugin.md, The plugin.");
        output.AppendLine();

        if (upgrade.PinMoves && upgrade.Pipeline is { } run)
        {
            // The pipeline is the way an upgrade lands once a release has written it (D-201): a run, not a session.
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{++step}. This solution {(upgrade.Pinned is { } from ? "pins " + from : "pins no release")}. Run its upgrade pipeline, {run}, with release {upgrade.Target}."));
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}It moves the pin, runs adoption and opens the pull request into {upgrade.Integration}; review and merge it."));
            output.AppendLine(Indent + "If 'sdlc stack' then lists rows under Not mentioned, '/sdlc:init' walks each one.");

            if (run == UpgradePipeline.PathOf(Platform.AzureDevOps).Replace('\\', '/') || run == Initialise.RetiredPipelines[2].Retired)
            {
                output.AppendLine(Indent + "It runs on the pool its variable SDLC_AGENT_POOL names: set it on the pipeline first, to your pool");
                output.AppendLine(Indent + "or to 'Azure Pipelines' for a Microsoft-hosted agent.");
            }
        }
        else if (upgrade.PinMoves)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{++step}. This solution {(upgrade.Pinned is { } from ? "pins " + from : "pins no release")}. On {upgrade.Integration}, up to date, this release moves it; an upgrade needs no cycle:"));
            Commands(output, $"git switch {upgrade.Integration}", "sdlc upgrade apply --push");
            output.AppendLine(Indent + "It commits the move on its own branch, checks it as the pull request will, and opens the");
            output.AppendLine(Indent + "pull request. Without --push it stops at the commit. If 'sdlc stack' lists rows under");
            output.AppendLine(Indent + "Not mentioned, '/sdlc:init' walks each one; that pull request may answer them.");
            output.AppendLine(Indent + "It writes the upgrade pipeline as well, so the next release is a run of it.");
        }
        else
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. This solution already pins {upgrade.Pinned}. Nothing to do."));
        }

        if (upgrade.Factory is not null)
        {
            output.AppendLine();
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. The factory's agent needs nothing: every run installs the release {upgrade.Integration} pins. Rebuild its"));
            output.AppendLine(Indent + "image only to move what the image itself carries: 'sdlc guidance show agent-image'.");
        }

        output.AppendLine();

        if (upgrade.Cycles.Count == 0)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. No open cycle pins an older release."));
        }
        else
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{++step}. Open cycles keep the release their branch pins until it takes the upgrade. Leaving one is allowed;"));
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}to move it, once the upgrade is on {upgrade.Integration}, merge {upgrade.Integration} into its branch:"));

            foreach (UpgradeChecklist.Behind cycle in upgrade.Cycles)
            {
                output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{cycle.Branch}  pins {cycle.Pinned}"));
            }
        }

        return output.ToString();
    }

    private static void Commands(StringBuilder output, params string[] commands)
    {
        foreach (string command in commands)
        {
            output.AppendLine(Indent + command);
        }
    }
}
