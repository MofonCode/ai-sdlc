using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace Sdlc.Core.Constraints;

/// <summary>
/// The stack rules the binary carries, and the subset a solution switches on.
/// </summary>
/// <remarks>
/// <para><strong>Embedded, for the reason every shipped document is embedded.</strong> D-33 keeps a
/// shipped profile inside the binary because an adoption has to work on a machine with no feed
/// configured and no network, and D-41 keeps the global library there because a file beside the binary
/// is a file that is missing in exactly the case nobody tests. A tier that resolved from a restore
/// could fail to resolve at a gate.</para>
///
/// <para><strong>A solution's declared stack says which of them bind.</strong> They used to follow
/// from a profile name in <c>solution.md</c>: recording <c>dotnet-default</c> brought every rule that
/// profile shipped, and nothing in the solution said so. Now the declared stack lists each rule with
/// whether it is on, and this hands back only those — so what a person reads in their own file is
/// what a plan is judged against (D-76).</para>
///
/// <para><strong>One document per profile still, found by its name</strong> — <c>dotnet-default</c>
/// resolves <c>dotnet-default.md</c> — so giving a stack its first rule is adding a file rather than
/// wiring anything. What changed is that ids are unique across every document rather than per
/// profile, because a solution names a rule by its id alone and two rules called <c>PRO_01</c> would
/// be one row switching on both.</para>
///
/// <para><strong>Unlike the global tier there is no file-wins fallback</strong>, so editing these
/// rules during the framework's own development needs a rebuild. The global library earns its lookup
/// chain by being edited constantly; these move rarely, and a second discovery order would be two ways
/// to answer one question. <see cref="FileConstraintDocuments"/> still takes an explicit path, which
/// is the seam an external rule package would arrive through.</para>
/// </remarks>
internal static class ShippedProfileLibrary
{
    private static readonly Regex _rulePattern = new(
        @"^##\s+(?<id>PRO_[0-9]{2,4})(?:\.[0-9]{1,3})?\s",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// What the stack-rule tier is recorded as having been read from, when it came from here.
    /// </summary>
    /// <remarks>
    /// Names the declared stack as well as the release, because the question behind "where did this
    /// rule come from" is what switched it on — and that is a row in a file the reader owns.
    /// </remarks>
    public static string Source { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"the rules .sdlc/{Cycles.Cycle.TechStackFileName} switches on, embedded in sdlc {Release.Version}");

    /// <summary>
    /// The rules one profile ships, or null when it ships none.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty string: a profile with no document has no rules, where an empty
    /// document would read as one that resolved and found nothing — which is the same output a dropped
    /// resource would produce.
    /// </remarks>
    /// <param name="profile">The profile's name.</param>
    /// <returns>The document's text, or null.</returns>
    public static string? Text(string? profile)
    {
        // Resolved through the profile set rather than by trusting the spelling handed in. That gives
        // the canonical name for the resource lookup -- resource names are case-sensitive and a
        // profile is written lower-case.
        if (Solutions.StackProfiles.Find(profile) is not { } found)
        {
            return null;
        }

        string resource = string.Create(
            CultureInfo.InvariantCulture,
            $"Sdlc.Core.Profiles.{found.Name}.md");

        using Stream? stream = typeof(ShippedProfileLibrary).Assembly.GetManifestResourceStream(resource);

        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Every rule this build ships that binds, across every profile's document.
    /// </summary>
    /// <remarks>
    /// What the stack draft lists and what <c>sdlc doctor</c> compares a declared stack against. A
    /// document that does not parse contributes nothing — the suite fails on one, so the alternative
    /// would be a refusal in the middle of a drafter over a defect a person cannot act on.
    /// </remarks>
    /// <returns>The rules in force, in the order the profiles are listed.</returns>
    public static IReadOnlyList<Constraint> Rules()
    {
        List<Constraint> rules = [];

        foreach (Solutions.StackProfile profile in Solutions.StackProfiles.All)
        {
            if (Text(profile.Name) is not { } text)
            {
                continue;
            }

            Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(text, Source);

            if (parsed.IsSuccess)
            {
                rules.AddRange(parsed.Value.Where(rule => rule.IsInForce));
            }
        }

        return rules;
    }

    /// <summary>
    /// How a rule is named in a declared stack: its tier and number, without a revision.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The id a stack row carries.</returns>
    public static string RowId(Constraint rule) => rule.Id.ToString().Split('.')[0];

    /// <summary>
    /// The shipped rules with every one a solution has not switched on taken out.
    /// </summary>
    /// <remarks>
    /// Filtered as text rather than after parsing, because what the tier hands on is a document: the
    /// parser, the renderer and the interrogation all read it as one, and a second shape for "some of
    /// a document" would be a second thing each of them had to understand. A rule's block runs from
    /// its heading to the next heading, so dropping one is dropping those lines.
    /// </remarks>
    /// <param name="on">The ids a declared stack switches on.</param>
    /// <returns>The document holding only those rules, or null when none of them is shipped.</returns>
    public static string? Selected(IReadOnlySet<string> on)
    {
        ArgumentNullException.ThrowIfNull(on);

        StringBuilder kept = new();
        bool any = false;

        foreach (Solutions.StackProfile profile in Solutions.StackProfiles.All)
        {
            if (Text(profile.Name) is not { } text)
            {
                continue;
            }

            bool keeping = true;

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                Match rule = _rulePattern.Match(trimmed);

                if (rule.Success)
                {
                    keeping = on.Contains(rule.Groups["id"].Value);
                    any |= keeping;
                }
                else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    keeping = true;
                }

                if (keeping)
                {
                    kept.Append(trimmed).Append('\n');
                }
            }
        }

        return any ? kept.ToString() : null;
    }
}
