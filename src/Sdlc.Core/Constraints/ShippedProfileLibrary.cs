using System.Globalization;
using FluentResults;

namespace Sdlc.Core.Constraints;

/// <summary>
/// The profile constraint libraries the binary carries, found by profile name.
/// </summary>
/// <remarks>
/// <para><strong>Embedded, for the reason every profile is embedded.</strong> D-33 keeps a shipped
/// profile inside the binary because an adoption has to work on a machine with no feed configured and
/// no network, and D-41 keeps the global library there because a file beside the binary is a file that
/// is missing in exactly the case nobody tests. A profile's rules are subject to both arguments at
/// once: they reach a solution the moment it records the profile's name, and a tier that resolved from
/// a restore could fail to resolve at a gate.</para>
///
/// <para><strong>A profile's rules are found by its name</strong> — <c>dotnet-default</c> resolves
/// <c>dotnet-default.md</c>. So giving a profile its first rule is adding a file rather than wiring
/// anything, and a profile with no file has no rules rather than an empty document: <c>ml-tensorflow</c>
/// is in that position deliberately, because nothing has been built from it yet and a rule written
/// ahead of a real solution would be a guess with the authority of a constraint.</para>
///
/// <para><strong>Unlike the global tier there is no file-wins fallback</strong>, so editing a profile's
/// rules during the framework's own development needs a rebuild. The global library earns its lookup
/// chain by being edited constantly; a profile carries three rules that move rarely, and a second
/// discovery order would be two ways to answer one question — which is how the hook ended up with a
/// front door and an open back door. <see cref="FileConstraintDocuments"/> still takes an explicit
/// path, which is the seam an external profile package would arrive through.</para>
/// </remarks>
internal static class ShippedProfileLibrary
{
    /// <summary>
    /// What a profile tier is recorded as having been read from, when it came from here.
    /// </summary>
    /// <remarks>
    /// Names the profile as well as the release, because the question behind "where did this rule come
    /// from" is which profile asserted it — and a solution reading its rules has already forgotten that
    /// <c>solution.md</c> is what selected them.
    /// </remarks>
    /// <param name="profile">The profile the rules belong to.</param>
    /// <returns>One line naming the profile and the release that carries it.</returns>
    public static string Source(string profile) => string.Create(
        CultureInfo.InvariantCulture,
        $"the {profile} profile, embedded in sdlc {Release.Version}");

    /// <summary>
    /// The rules a profile ships, or null when it ships none.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty string, and the difference is what <c>sdlc constraints</c> prints: a
    /// profile with no rules has no tier line at all, where an empty document would read as a tier that
    /// resolved and found nothing — which is the same output a dropped resource would produce.
    /// </remarks>
    /// <param name="profile">The profile's name, as <c>solution.md</c> records it.</param>
    /// <returns>The document's text, or null.</returns>
    public static string? Text(string? profile)
    {
        // Resolved through the profile set rather than by trusting the spelling in the file. That gives
        // the canonical name for the resource lookup -- resource names are case-sensitive and a profile
        // is written lower-case -- and it means a solution recording a profile nothing answers to gets
        // no rules, which is the same answer adoption and `draft profile` give it.
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
    /// How many rules a profile ships that bind.
    /// </summary>
    /// <remarks>
    /// For telling somebody what a profile migration costs them in rules, which is the half of that
    /// move nothing used to mention. A document that does not parse counts nothing — the suite fails on
    /// one, so the alternative would be a refusal in the middle of a drafter over a defect a person
    /// cannot act on.
    /// </remarks>
    /// <param name="profile">The profile's name.</param>
    /// <returns>The number of rules in force, which is zero for a profile shipping no document.</returns>
    public static int Count(string? profile)
    {
        if (Text(profile) is not { } text)
        {
            return 0;
        }

        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(text, Source(profile!));

        return parsed.IsSuccess
            ? parsed.Value.Count(rule => rule.IsInForce)
            : 0;
    }
}
