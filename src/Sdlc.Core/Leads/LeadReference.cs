using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Leads;

/// <summary>Where a lead comes from.</summary>
public enum LeadSource
{
    /// <summary><c>azuredevops:1234</c> — an Azure DevOps work item: a bug, a user story.</summary>
    AzureDevOps,

    /// <summary><c>github:56</c> — a GitHub issue in this repository.</summary>
    GitHub,

    /// <summary><c>jira:PROJ-123</c> — a Jira issue.</summary>
    Jira,

    /// <summary><c>suggestion:&lt;cycle&gt;/&lt;finding&gt;</c> — an open non-blocking finding.</summary>
    Suggestion,

    /// <summary>Anything else: a file a person wrote down.</summary>
    File,
}

/// <summary>
/// A reference to something a person can hand <c>intent</c> to begin a cycle (D-89).
/// </summary>
/// <remarks>
/// <para><strong>A prefix spelled in full, and only these.</strong> <c>azuredevops:</c>, <c>github:</c>,
/// <c>jira:</c> and <c>suggestion:</c> are read as sources; anything else is a path. So
/// <c>C:\notes\bug.md</c> is a file, not a source called <c>C</c>, and a reader never decodes an
/// abbreviation.</para>
/// <para><strong>A known prefix with a malformed value is refused, not read as a path.</strong>
/// <c>github:abc</c> was meant as an issue, and reading it as a file nobody has would hide the typo.</para>
/// </remarks>
/// <param name="Source">Where it comes from.</param>
/// <param name="Value">What follows the prefix: an id, a key, a cycle and finding, or a path.</param>
public sealed record LeadReference(LeadSource Source, string Value)
{
    private static readonly Regex _jira = new(
        "^[A-Z][A-Z0-9_]+-[0-9]+$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>The prefixes read as sources, as they are written.</summary>
    public static IReadOnlyList<string> Prefixes { get; } = ["azuredevops:", "github:", "jira:", "suggestion:"];

    /// <summary>The reference as it is written.</summary>
    /// <returns>The prefix and value, or the path alone for a file.</returns>
    public override string ToString() => Source switch
    {
        LeadSource.AzureDevOps => "azuredevops:" + Value,
        LeadSource.GitHub => "github:" + Value,
        LeadSource.Jira => "jira:" + Value,
        LeadSource.Suggestion => "suggestion:" + Value,
        _ => Value,
    };

    /// <summary>
    /// Reads a reference.
    /// </summary>
    /// <param name="text">The reference as given.</param>
    /// <param name="reference">The reference, when this returns true.</param>
    /// <param name="problem">Why it is refused, when this returns false.</param>
    /// <returns>True when it is a well-formed reference.</returns>
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out LeadReference? reference,
        [NotNullWhen(false)] out string? problem)
    {
        reference = null;
        problem = null;
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            problem = "No lead was given. Give one as azuredevops:1234, github:56, jira:PROJ-123, suggestion:<cycle>/<finding>, or a path.";
            return false;
        }

        foreach (string prefix in Prefixes)
        {
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string value = trimmed[prefix.Length..].Trim();
            (LeadSource source, bool sound, string expected) = prefix switch
            {
                "azuredevops:" => (LeadSource.AzureDevOps, IsNumber(value), "a work item number, as in azuredevops:1234"),
                "github:" => (LeadSource.GitHub, IsNumber(value.TrimStart('#')), "an issue number, as in github:56"),
                "jira:" => (LeadSource.Jira, _jira.IsMatch(value), "an issue key, as in jira:PROJ-123"),
                _ => (LeadSource.Suggestion, value.Split('/').Length is 2 or 3 && !value.Split('/').Any(string.IsNullOrWhiteSpace), "a cycle and a finding, as in suggestion:20261015_1930_45/F2"),
            };

            if (!sound)
            {
                problem = string.Create(CultureInfo.InvariantCulture, $"'{trimmed}' is not {expected}.");
                return false;
            }

            reference = new LeadReference(source, source == LeadSource.GitHub ? value.TrimStart('#') : value);
            return true;
        }

        reference = new LeadReference(LeadSource.File, trimmed);
        return true;
    }

    private static bool IsNumber(string value) =>
        value.Length > 0 && value.All(char.IsAsciiDigit);
}
