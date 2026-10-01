using System.Globalization;
using System.Text;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Approvals;

/// <summary>
/// Renders what a person reads before approving a cycle.
/// </summary>
/// <remarks>
/// The entry they will paste, the state of the three documents it covers, and what to do with it.
/// It lives beside the model and is asserted by the suite for the same reason the constraint renderer
/// is: a skill shows this output verbatim, so its shape is a contract rather than presentation.
/// </remarks>
public static class ApprovalRenderer
{
    /// <summary>
    /// Renders the approval text and its context.
    /// </summary>
    /// <param name="text">What the handler produced.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ApprovalTextResult text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder output = new();
        CycleStatus status = text.Status;

        output.AppendLine(CultureInfo.InvariantCulture, $"Cycle {status.Cycle.Id}");
        output.AppendLine();

        // The three documents with their round history and their hash. A person cannot verify a pasted
        // hash by reading it, so showing the first twelve characters here is what makes a later
        // mismatch diagnosable: the gate names the file and shows the same twelve.
        foreach (ArtifactState artifact in status.Artifacts)
        {
            string rounds = artifact.History.Count == 0
                ? "never interrogated"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"r{artifact.LatestRound}, {Blockers(artifact.OpenBlockers.Count)}");

            output.AppendLine(CultureInfo.InvariantCulture,
                $"    {Cycle.NameOf(artifact.Kind),-8}{ContentHash.Abbreviate(artifact.Hash ?? "missing"),-14}{rounds}");
        }

        IReadOnlyList<string> accepted = status.OpenBlockerIds;
        if (accepted.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Open blocking findings, which the entry below accepts by name:");
            output.AppendLine();

            foreach (ArtifactState artifact in status.Artifacts)
            {
                foreach (Findings.Finding finding in artifact.OpenBlockers)
                {
                    output.AppendLine(CultureInfo.InvariantCulture,
                        $"    {Cycle.NameOf(artifact.Kind)}.{finding.Id}  {finding.Cites}");
                    output.AppendLine(CultureInfo.InvariantCulture,
                        $"        {finding.Statement}");
                }
            }
        }

        output.AppendLine();
        output.AppendLine("Paste this:");
        output.AppendLine();
        output.Append(text.Entry);
        output.AppendLine();
        output.AppendLine(text.Instructions);

        return output.ToString();
    }

    private static string Blockers(int count) => count switch
    {
        0 => "clean",
        1 => "1 blocking",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} blocking"),
    };
}
