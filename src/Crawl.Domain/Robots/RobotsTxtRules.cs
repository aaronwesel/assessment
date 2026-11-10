namespace Crawl.Domain.Robots;

/// <summary>
/// Minimal robots.txt model: User-agent group matching, Allow/Disallow longest-prefix,
/// and optional Crawl-delay (seconds). Deterministic; not AI-assisted.
/// </summary>
public sealed class RobotsTxtRules
{
    private readonly IReadOnlyList<RobotsGroup> _groups;

    public RobotsTxtRules(IReadOnlyList<RobotsGroup> groups) => _groups = groups;

    public static RobotsTxtRules Empty { get; } = new([]);

    public static RobotsTxtRules Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Empty;

        var groups = new List<RobotsGroup>();
        var pendingAgents = new List<string>();
        var currentAgents = new List<string>();
        var allows = new List<string>();
        var disallows = new List<string>();
        double? crawlDelay = null;
        var inRuleBlock = false;

        void Flush()
        {
            if (currentAgents.Count == 0)
                return;
            groups.Add(new RobotsGroup(
                currentAgents.ToArray(),
                allows.ToArray(),
                disallows.ToArray(),
                crawlDelay));
            currentAgents.Clear();
            allows.Clear();
            disallows.Clear();
            crawlDelay = null;
            inRuleBlock = false;
        }

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var hash = line.IndexOf('#');
            if (hash >= 0)
                line = line[..hash].TrimEnd();

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            if (key.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (inRuleBlock)
                {
                    Flush();
                    pendingAgents.Clear();
                }

                pendingAgents.Add(value.ToLowerInvariant());
                continue;
            }

            if (pendingAgents.Count == 0 && currentAgents.Count == 0)
                continue;

            if (!inRuleBlock)
            {
                currentAgents.AddRange(pendingAgents);
                pendingAgents.Clear();
                inRuleBlock = true;
            }

            if (key.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                allows.Add(value);
            }
            else if (key.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
            {
                disallows.Add(value);
            }
            else if (key.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase)
                     && double.TryParse(
                         value,
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out var delay)
                     && delay >= 0)
            {
                crawlDelay = delay;
            }
        }

        Flush();
        return new RobotsTxtRules(groups);
    }

    public bool IsAllowed(string userAgent, Uri url)
    {
        var group = FindGroup(userAgent);
        if (group is null)
            return true;

        var path = string.IsNullOrEmpty(url.AbsolutePath) ? "/" : url.AbsolutePath;
        if (!string.IsNullOrEmpty(url.Query))
            path += url.Query;

        // Longest matching Allow/Disallow prefix wins; Allow wins ties.
        var bestAllow = LongestPrefixMatch(group.Allow, path);
        var bestDisallow = LongestPrefixMatch(group.Disallow, path);

        if (bestAllow < 0 && bestDisallow < 0)
            return true;
        return bestAllow >= bestDisallow;
    }

    public TimeSpan? GetCrawlDelay(string userAgent)
    {
        var group = FindGroup(userAgent);
        return group?.CrawlDelaySeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private RobotsGroup? FindGroup(string userAgent)
    {
        var ua = userAgent.ToLowerInvariant();
        RobotsGroup? star = null;
        RobotsGroup? specific = null;
        var specificLen = -1;

        foreach (var group in _groups)
        {
            foreach (var agent in group.UserAgents)
            {
                if (agent == "*")
                {
                    star ??= group;
                    continue;
                }

                if (ua.Contains(agent, StringComparison.Ordinal) && agent.Length > specificLen)
                {
                    specific = group;
                    specificLen = agent.Length;
                }
            }
        }

        return specific ?? star;
    }

    private static int LongestPrefixMatch(IReadOnlyList<string> rules, string path)
    {
        var best = -1;
        foreach (var rule in rules)
        {
            // Empty Disallow means allow all for that directive — skip as a restriction.
            if (rule.Length == 0)
                continue;
            if (path.StartsWith(rule, StringComparison.Ordinal) && rule.Length > best)
                best = rule.Length;
        }

        return best;
    }
}

public sealed record RobotsGroup(
    IReadOnlyList<string> UserAgents,
    IReadOnlyList<string> Allow,
    IReadOnlyList<string> Disallow,
    double? CrawlDelaySeconds);
