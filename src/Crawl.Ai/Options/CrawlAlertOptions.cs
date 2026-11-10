namespace Crawl.Ai.Options;

/// <summary>
/// Optional email alerts for crawl incidents. Bound from "Crawl:Alerts" (also Crawl__Alerts__*).
/// Never put passwords or PHI in alert bodies.
/// </summary>
public sealed class CrawlAlertOptions
{
    public const string SectionName = "Crawl:Alerts";

    /// <summary>Feature flag — off by default.</summary>
    public bool EmailEnabled { get; set; }

    /// <summary>Prefer SES-shaped settings when set; otherwise SMTP.</summary>
    public string Provider { get; set; } = "smtp"; // smtp | ses | stub

    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    /// <summary>Prefer env Crawl__Alerts__SmtpPassword — never commit.</summary>
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;

    public string? SesRegion { get; set; }
}
