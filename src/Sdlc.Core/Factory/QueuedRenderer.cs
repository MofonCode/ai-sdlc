using System.Globalization;
using System.Text;

namespace Sdlc.Core.Factory;

/// <summary>Renders the factory's queue for a person and for the batch-build skill.</summary>
/// <remarks>
/// One cycle per line, id first, so a skill can take the ids from the queued block and a person can read
/// the reason beside every cycle that was passed over.
/// </remarks>
public static class QueuedRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders the queue.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ListQueuedCycles.Response queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Queued for a build: {queue.Cycles.Count}"));

        foreach (ListQueuedCycles.Queued cycle in queue.Cycles)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{cycle.Id}  {cycle.Headline ?? "(no headline)"}"));
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Held back: {queue.Held.Count}"));

        foreach (ListQueuedCycles.HeldBack cycle in queue.Held)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{cycle.Id}  {cycle.Headline ?? "(no headline)"}"));
            output.AppendLine(Indent + Indent + "  " + cycle.Reason);
        }

        if (!queue.RemoteRead)
        {
            output.AppendLine();
            output.AppendLine(
                "The remote's branches could not be read, so no cycle is held back by one. A cycle somebody "
                + "has already taken may be listed as queued.");
        }

        return output.ToString();
    }
}
