# Guided crawl UX (product deliverables)

Assessments PDF extras on top of the graded .NET crawl core: landing, wizard, billing hints, Job Details value.

## Surfaces

| Route | Purpose |
|-------|---------|
| `/` | Crawl product landing — logo, tagline, **per-page price** from `GET /api/v1/growth/crawl-pricing` |
| `/crawl/welcome` | AI-guided wizard — friendly URL coercion, depth, review → `POST` Crawl.Api `/api/jobs` |
| `/crawl` | Quick start (anonymous OK) + wizard link |
| `/crawl/jobs/:id` | **P0** report: DLR stats, export, diagnosis, retry, live tree, element pick, AI panels |
| `/register` · `/login` | Save jobs + Stripe test billing (`/billing`) |

## Job Details (completed jobs must feel useful)

1. **Crawl report** — outcome badge, page counts, avg/min/max DLR, test per-page cost estimate.
2. **Empty / weak diagnosis** — deterministic hints + auto **AI summarize** when outcome is not healthy.
3. **Export** — CSV, JSON, NDJSON from the page tree (client-side; values from `.NET`).
4. **Retry** — wizard deep links for deeper crawl or login-assist path.
5. **Page tree** — loads during **live** crawl (not gated until terminal).
6. **Element pick** — choose page, paste HTML, suggest CSS/XPath, export JSON selection.
7. **Assistive AI** — existing panels when tree has pages.

## API additions

| Method | Path | Notes |
|--------|------|--------|
| `GET` | `/api/jobs/{id}/pages` | Flat page list (export-friendly) |
| `GET` | `/api/v1/growth/crawl-pricing` | Public test pricing; env `CRAWL_PRICE_PER_PAGE_CENTS` (default `2`) |

## Stripe (test)

- Subscription Checkout remains on `/billing` (platform FastAPI).
- Landing/wizard show **metered per-page** display when `STRIPE_METERED_PRICE_ID` is set; fulfillment still webhook-driven.

## Smoke

```bash
docker compose up --build
# Landing → Guided setup → scrape demo URL → Job Details → Export CSV
curl -s http://localhost:8000/api/v1/growth/crawl-pricing
curl -s http://localhost:8080/api/jobs/{jobId}/pages
```

## README alignment

**First:** crawl core (domain, API, worker, React Start/Details/History).  
**This doc:** product UX on the same branch.  
**Cut:** live metered Stripe production, SSRF hardening.  
**Next:** bind jobs to JWT users, real usage records, payer portal adapters.
