namespace Crawl.Domain.Scheduling;

/// <summary>
/// Outcome of an atomic page claim under at-least-once delivery and multi-worker races.
/// </summary>
public enum PageClaimResult
{
    /// <summary>This worker owns the page and should fetch/process it.</summary>
    Claimed = 0,

    /// <summary>Page already terminal — ack duplicate/redelivery without work.</summary>
    AlreadyTerminal = 1,

    /// <summary>Another worker currently owns Processing (non-redelivery duplicate).</summary>
    OwnedByOther = 2,

    /// <summary>Job or page missing — drop message.</summary>
    Missing = 3
}
