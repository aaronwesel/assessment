#!/usr/bin/env bash
# E2E gate: multi-site crawls + API surfaces for Trailmap UX redesign.
set -euo pipefail
API="${CRAWL_API_URL:-http://127.0.0.1:8080}"
PREVIEW="${FRONTEND_URL:-http://127.0.0.1:4173}"
GROWTH="${GROWTH_API_URL:-http://127.0.0.1:8000}"
FAIL=0

log() { echo "[e2e] $*"; }
fail() { log "FAIL: $*"; FAIL=1; }

curl -fsS "$API/health" >/dev/null || { fail "crawl-api health"; exit 1; }
curl -fsS "$PREVIEW/" >/dev/null || fail "frontend preview"
curl -fsS "$GROWTH/api/v1/growth/crawl-pricing" | grep -q price_per_page_cents || fail "growth pricing"

curl -fsS -X POST "$API/api/ai/setup/preview" -H 'Content-Type: application/json' \
  -d '{"url":"https://books.toscrape.com","maxDepth":2,"wizardStep":1}' \
  | python3 -c "import sys,json; j=json.load(sys.stdin); assert j.get('urlValid') and j.get('reachable'), j" \
  || fail "setup preview"

curl -fsS -X POST "$API/api/ai/setup/preview" -H 'Content-Type: application/json' \
  -d '{"url":"https://books.toscrape.com","maxDepth":2,"wizardStep":1,"question":"is depth 2 ok?"}' \
  | python3 -c "import sys,json; j=json.load(sys.stdin); body=json.dumps(j); assert j.get('modelAnswer'), j; assert 'DEEPSEEK_API_KEY' not in body and 'CrawlAi__' not in body, body" \
  || fail "setup preview ask (no raw env names)"

curl -fsS "$API/api/ai/features" \
  | python3 -c "import sys,json; j=json.load(sys.stdin); assert 'deepSeekConfigured' in j, j" \
  || fail "ai features"

wait_job() {
  local id="$1" max="${2:-120}"
  local i=0 status=""
  while [ "$i" -lt "$max" ]; do
    status=$(curl -fsS "$API/api/jobs/$id" | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
    case "$status" in Completed|Failed|Canceled) echo "$status"; return 0 ;; esac
    sleep 2
    i=$((i+2))
  done
  echo "timeout"
  return 1
}

count_tree() {
  python3 -c "
import sys,json
t=json.load(sys.stdin)
def walk(n):
  if not n: return 0
  return 1+sum(walk(c) for c in (n.get('children') or []))
print(walk(t.get('root')))
"
}

for site in \
  "https://example.com" \
  "https://books.toscrape.com" \
  "https://quotes.toscrape.com" \
  "https://www.scrapethissite.com/pages/"
do
  log "crawl $site"
  job=$(curl -fsS -X POST "$API/api/jobs" -H 'Content-Type: application/json' \
    -d "{\"url\":\"$site\",\"maxDepth\":2}" | python3 -c "import sys,json; print(json.load(sys.stdin)['jobId'])")
  st=$(wait_job "$job" 180) || fail "$site wait"
  [ "$st" = Completed ] || fail "$site status=$st job=$job"
  tree_count=$(curl -fsS "$API/api/jobs/$job/tree" | count_tree)
  log "$site job=$job status=$st tree_nodes=$tree_count"
  if [ "$site" = "https://example.com" ]; then
    [ "$tree_count" -ge 1 ] || fail "example.com tree"
  else
    [ "$tree_count" -ge 3 ] || fail "$site expected multi-page tree got $tree_count"
  fi
  dlr=$(curl -fsS "$API/api/jobs/$job/tree" | python3 -c "
import sys,json
def first_dlr(n):
  if not n: return None
  if n.get('domainLinkRatio') is not None: return n['domainLinkRatio']
  for c in n.get('children') or []:
    v=first_dlr(c)
    if v is not None: return v
  return None
print(first_dlr(json.load(sys.stdin).get('root')))
")
  [ "$dlr" != "None" ] && [ -n "$dlr" ] || fail "$site missing DLR on tree"
done

curl -fsS "$API/api/jobs?page=1&pageSize=5" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j.get('items'), j" \
  || fail "history list"

if [ "$FAIL" -eq 0 ]; then log "ALL E2E CHECKS PASSED"; else log "E2E GATE FAILED"; exit 1; fi
