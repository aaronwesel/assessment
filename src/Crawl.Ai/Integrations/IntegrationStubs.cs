using Crawl.Ai.FieldFinder;
using Crawl.Ai.Models;

namespace Crawl.Ai.Integrations;

/// <summary>Payer-portal shaped client — stub for eligibility / portal session patterns.</summary>
public interface IPayerPortalClient
{
    Task<PayerEligibilityStubResult> CheckEligibilityAsync(
        PayerEligibilityStubRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Clearinghouse-shaped client — stub for claim submission patterns (not real X12).</summary>
public interface IClearinghouseClient
{
    Task<ClearinghouseStubResult> SubmitClaimStubAsync(
        ClearinghouseStubRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PayerEligibilityStubRequest(
    string PayerId,
    string MemberId,
    string? Npi,
    Guid? CrawlJobId,
    string? HtmlSnapshot);

public sealed record PayerEligibilityStubResult(
    string Status,
    string Message,
    IReadOnlyList<FieldSuggestion> SuggestedFields,
    bool RequiresOperatorConfirmation);

public sealed record ClearinghouseStubRequest(
    string TradingPartnerId,
    string ClaimControlNumber,
    IDictionary<string, string> Fields);

public sealed record ClearinghouseStubResult(
    string Status,
    string Message,
    string CorrelationId);

/// <summary>
/// Fake eligibility adapter that optionally calls the AI field finder on an HTML snapshot.
/// Documented as pattern-only — not a certified EDI eligibility transaction.
/// </summary>
public sealed class StubPayerPortalClient : IPayerPortalClient
{
    private readonly IFieldFinderService _fieldFinder;

    public StubPayerPortalClient(IFieldFinderService fieldFinder)
    {
        _fieldFinder = fieldFinder;
    }

    public async Task<PayerEligibilityStubResult> CheckEligibilityAsync(
        PayerEligibilityStubRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FieldSuggestion> suggestions = Array.Empty<FieldSuggestion>();
        if (!string.IsNullOrWhiteSpace(request.HtmlSnapshot) && request.CrawlJobId is Guid jobId)
        {
            var finder = await _fieldFinder.SuggestAsync(
                new FieldFinderRequest(
                    jobId,
                    null,
                    null,
                    request.HtmlSnapshot!,
                    "eligibility member npi payer"),
                cancellationToken).ConfigureAwait(false);
            suggestions = finder.Suggestions;
        }

        return new PayerEligibilityStubResult(
            "stub_accepted",
            $"Stub eligibility check queued for payer={request.PayerId} member={Mask(request.MemberId)}. Not a real EDI/X12 call.",
            suggestions,
            RequiresOperatorConfirmation: true);
    }

    private static string Mask(string memberId)
    {
        if (string.IsNullOrEmpty(memberId) || memberId.Length < 4)
        {
            return "***";
        }

        return new string('*', memberId.Length - 4) + memberId[^4..];
    }
}

public sealed class StubClearinghouseClient : IClearinghouseClient
{
    public Task<ClearinghouseStubResult> SubmitClaimStubAsync(
        ClearinghouseStubRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ClearinghouseStubResult(
            "stub_queued",
            $"Claim {request.ClaimControlNumber} accepted by clearinghouse stub for partner {request.TradingPartnerId}. Not a real 837.",
            Guid.NewGuid().ToString("N")));
    }
}
