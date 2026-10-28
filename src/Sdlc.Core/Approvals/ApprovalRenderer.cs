using System.Globalization;
using System.Text;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Approvals;

/// <summary>
/// Renders what a person reads before recording a gate decision.
/// </summary>
/// <remarks>
/// The entry they will paste, the state of the three documents it covers, and what to do with it.
/// It lives beside the model and is asserted by the suite for the same reason the constraint renderer
/// is: a skill shows this output verbatim, so its shape is a contract rather than presentation.
/// </remarks>
public static class ApprovalRenderer
{
    /// <summary>
    /// Renders the ledger entry and its context.
    /// </summary>
    /// <param name="text">What the handler produced.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ApprovalText.Response text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder output = new();
        ReadCycleStatus.Response status = text.Status;

        output.AppendLine(CultureInfo.InvariantCulture,
            $"Cycle {status.Cycle.Id} - {Heading(text.Decision)}");
        output.AppendLine();

        // The three documents with their round history and their hash. A person cannot verify a pasted
        // hash by reading it, so showing the first twelve characters here is what makes a later
        // mismatch diagnosable: the gate names the file and shows the same twelve.
        foreach (ReadCycleStatus.ArtifactState artifact in status.Artifacts)
        {
            string rounds = artifact.History.Count == 0
                ? "never interrogated"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"r{artifact.LatestRound}, {Blockers(artifact.OpenBlockers.Count)}");

            output.AppendLine(CultureInfo.InvariantCulture,
                $"    {Cycle.NameOf(artifact.Kind),-8}{ContentHash.Abbreviate(artifact.Hash ?? "missing"),-14}{rounds}");
        }

        // Reached only on a refusal: an approval with an open blocker never produces an entry. A person
        // refusing is the one who most needs these on the page, because they are what the reason is
        // usually about.
        if (status.OpenBlockerIds.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Open blocking findings, which no decision can waive:");
            output.AppendLine();

            foreach (ReadCycleStatus.ArtifactState artifact in status.Artifacts)
            {
                foreach (Findings.Finding finding in artifact.OpenBlockers)
                {
                    output.AppendLine(CultureInfo.InvariantCulture,
                        $"    {Cycle.NameOf(artifact.Kind)}.{finding.Id}  {finding.Cites?.Id}");

                    if (finding.Cites?.Line() is { } rule)
                    {
                        output.AppendLine(CultureInfo.InvariantCulture, $"        {rule}");
                    }

                    output.AppendLine(CultureInfo.InvariantCulture,
                        $"        {finding.Headline}");

                    if (finding.Remedy is { } remedy)
                    {
                        output.AppendLine(CultureInfo.InvariantCulture,
                            $"        Fix: {remedy.ReplaceLineEndings(" ")}");
                    }
                }
            }
        }

        // The whole file, not the entry alone. A person reads what they are about to put in place, and
        // what they read is byte-for-byte what is on disk waiting for the rename -- so there is nothing
        // to transcribe and no second indentation to reconcile.
        output.AppendLine();
        output.AppendLine(text.CarriesForward > 0
            ? "This file, which carries the decisions already recorded:"
            : "This file:");
        output.AppendLine();
        output.Append(text.FileText);
        output.AppendLine();
        output.AppendLine(text.Instructions);

        return output.ToString();
    }

    private static string Heading(GateDecision decision) => decision switch
    {
        GateDecision.Approved => "approving",
        GateDecision.ChangesRequested => "requesting changes",
        GateDecision.Denied => "denying",
        GateDecision.Reopened => "reopening a standing approval",
        _ => "deciding",
    };

    private static string Blockers(int count) => count switch
    {
        0 => "clean",
        1 => "1 blocking",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} blocking"),
    };
}
