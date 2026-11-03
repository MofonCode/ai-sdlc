using System.Globalization;
using Sdlc.UsageReport;

// Reads Claude Code's transcripts and prints what they cost: per day, per role, and the sessions that cost
// the most. Read-only.
//
// Why this exists: two weeks of one solution's threads cost what they cost, and finding out where it went
// took a scratchpad parser over 640 MB of transcripts. Most of it was conversation, not judging -- a
// session carried past its stage, rebuilding its context after every pause -- and that is invisible to the
// per-cycle usage record, which counts the readers alone (D-121). This is the session-level view.
//
// It reads one host's transcript format, which is why it is a tool and not the binary (D-46).
string? project = null;
DateTimeOffset? since = null;
int top = 15;
string? csv = null;
string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

for (int at = 0; at < args.Length; at++)
{
    string? value = at + 1 < args.Length ? args[at + 1] : null;

    switch (args[at])
    {
        case "--project" when value is not null:
            project = value;
            at++;
            break;
        case "--since" when value is not null
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset from):
            since = from;
            at++;
            break;
        case "--top" when value is not null && int.TryParse(value, CultureInfo.InvariantCulture, out int count) && count >= 0:
            top = count;
            at++;
            break;
        case "--csv" when value is not null:
            csv = Path.GetFullPath(value);
            at++;
            break;
        case "--root" when value is not null:
            root = Path.GetFullPath(value);
            at++;
            break;
        default:
            Console.Error.WriteLine(
                "usage: usage-report [--project <name>] [--since <yyyy-MM-dd>] [--top <n>] [--csv <file>] [--root <dir>]\n\n"
                + "Prints what Claude Code's transcripts cost, per day, per role and per session. --project keeps the\n"
                + "folders whose name contains it; --root points at a projects directory other than ~/.claude/projects.\n"
                + "Weighted tokens: input x1, cache write x1.25, cache read x0.1, output x5.");
            return 2;
    }
}

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"There is no transcripts directory at {root}.");
    return 1;
}

List<SessionUsage> sessions = [.. Transcripts.Read(root, project)
    .Where(session => since is null || (session.Start is { } start && start >= since))];

if (sessions.Count == 0)
{
    Console.WriteLine($"No transcript under {root} matched{(project is null ? string.Empty : " '" + project + "'")}.");
    return 0;
}

double total = sessions.Sum(session => session.Weighted);

Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"{sessions.Count:N0} transcripts, {Report.Tokens(total)} weighted tokens{(project is null ? string.Empty : " in folders matching '" + project + "'")}."));

Report.Table(
    "By day",
    ["day", "transcripts", "weighted", "share"],
    sessions
        .GroupBy(session => session.Start?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "unknown")
        .OrderBy(day => day.Key, StringComparer.Ordinal)
        .Select(day => new[] { day.Key, day.Count().ToString("N0", CultureInfo.InvariantCulture), Report.Tokens(day.Sum(s => s.Weighted)), Report.Share(day.Sum(s => s.Weighted), total) }));

Report.Table(
    "By role",
    ["role", "transcripts", "calls", "median context", "weighted", "share"],
    sessions
        .GroupBy(session => session.Role)
        .OrderByDescending(role => role.Sum(s => s.Weighted))
        .Select(role => new[]
        {
            role.Key,
            role.Count().ToString("N0", CultureInfo.InvariantCulture),
            role.Sum(s => s.Calls).ToString("N0", CultureInfo.InvariantCulture),
            Report.Tokens(Report.Median(role.Select(s => (double)s.MaxContext))),
            Report.Tokens(role.Sum(s => s.Weighted)),
            Report.Share(role.Sum(s => s.Weighted), total),
        }));

if (top > 0)
{
    Report.Table(
        "The costliest " + top.ToString(CultureInfo.InvariantCulture),
        ["started", "role", "calls", "max context", "weighted", "opens with"],
        sessions
            .OrderByDescending(session => session.Weighted)
            .Take(top)
            .Select(session => new[]
            {
                session.Start?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "unknown",
                session.Role,
                session.Calls.ToString("N0", CultureInfo.InvariantCulture),
                Report.Tokens(session.MaxContext),
                Report.Tokens(session.Weighted),
                session.Head.Length > 60 ? session.Head[..60] : session.Head,
            }));
}

if (csv is not null)
{
    File.WriteAllLines(csv, Report.Csv(sessions));
    Console.WriteLine();
    Console.WriteLine("Every transcript, one row each: " + csv);
}

return 0;
