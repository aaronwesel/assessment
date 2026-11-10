# Crawl.Ai — assistive overlays

Operator-facing AI for crawl jobs. **Hard rule:** this library must **not** parse crawl links or compute Domain Link Ratio. Those stay in `Crawl.Domain` / Worker.

## Wire into Crawl.Api (additive)

```csharp
builder.Services.AddCrawlAi(builder.Configuration);
// ...
app.MapCrawlAi(); // or register CrawlAiController
```

Config section `CrawlAi` + env `DEEPSEEK_API_KEY` (graceful heuristic mode when missing).

## Endpoints (`/api/ai`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/features` | Feature flags + DeepSeek configured |
| POST | `/jobs/{jobId}/field-finder` | Suggest extraction targets |
| POST | `/jobs/{jobId}/login-assist/detect` | Detect login form + guided steps |
| POST | `/jobs/{jobId}/login-assist/confirm-step` | Operator approve step (vault ref only) |
| GET | `/jobs/{jobId}/login-assist/audit` | Audit trail |
| POST | `/jobs/{jobId}/summarize` | Result summarization |
| POST | `/jobs/{jobId}/ask` | Ask-the-crawl |
| POST | `/jobs/{jobId}/agent/plan` | Agentic plan (human confirm) |
| POST | `/jobs/{jobId}/agent/confirm` | Execute approved tools |
| GET | `/jobs/{jobId}/incidents` | Stretch: incidents + last remediation |
| POST | `/integrations/payer/eligibility-stub` | Payer portal stub |
| POST | `/integrations/clearinghouse/claim-stub` | Clearinghouse stub |

### Stretch — incident monitor

`CrawlAi:IncidentMonitorEnabled` + worker-emitted faults → DeepSeek remediation agent → backoff/politeness/`IEgressProxyProvider` (fail closed) + optional `Crawl:Alerts:EmailEnabled`. See `docs/crawl-readme.md`.

## Frontend

React panels: `frontend/src/crawl/ai/AiAssistPanels.tsx` — mount on Job Details (includes Incidents / remediations).
