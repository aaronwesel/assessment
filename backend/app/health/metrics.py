"""
In-process ops metrics for a tiny SLO view.

Not a Prometheus replacement — a lightweight counter/histogram stub that
`/health/metrics` and `/api/v1/health/slo` can expose for the superadmin
dashboard (workstream C) without requiring a sidecar.

HIPAA: metric labels must stay non-PHI (route templates, status classes).
"""

from __future__ import annotations

import threading
import time
from collections import defaultdict
from dataclasses import dataclass, field
from typing import Any


@dataclass
class _Counters:
    requests_total: int = 0
    errors_5xx: int = 0
    errors_4xx: int = 0
    by_route: dict[str, int] = field(default_factory=lambda: defaultdict(int))
    latencies_ms: list[float] = field(default_factory=list)
    started_at: float = field(default_factory=time.time)


_lock = threading.Lock()
_state = _Counters()
_MAX_LATENCY_SAMPLES = 500


def reset_metrics_for_tests() -> None:
    global _state
    with _lock:
        _state = _Counters()


def record_request(*, route: str, status_code: int, latency_ms: float) -> None:
    """Record one completed request. Route should be a template, not raw path with ids."""
    with _lock:
        _state.requests_total += 1
        _state.by_route[route] += 1
        if 500 <= status_code <= 599:
            _state.errors_5xx += 1
        elif 400 <= status_code <= 499:
            _state.errors_4xx += 1
        _state.latencies_ms.append(float(latency_ms))
        if len(_state.latencies_ms) > _MAX_LATENCY_SAMPLES:
            _state.latencies_ms = _state.latencies_ms[-_MAX_LATENCY_SAMPLES:]


def snapshot() -> dict[str, Any]:
    with _lock:
        samples = list(_state.latencies_ms)
        total = _state.requests_total
        e5 = _state.errors_5xx
        e4 = _state.errors_4xx
        by_route = dict(_state.by_route)
        uptime = time.time() - _state.started_at

    p95 = None
    avg = None
    if samples:
        ordered = sorted(samples)
        idx = min(len(ordered) - 1, int(len(ordered) * 0.95))
        p95 = round(ordered[idx], 2)
        avg = round(sum(ordered) / len(ordered), 2)

    availability = None
    if total:
        availability = round(1.0 - (e5 / total), 6)

    return {
        "uptime_seconds": round(uptime, 1),
        "requests_total": total,
        "errors_4xx": e4,
        "errors_5xx": e5,
        "availability": availability,
        "latency_ms_avg": avg,
        "latency_ms_p95": p95,
        "by_route": by_route,
        # Simple SLO targets for the ops view (not contractual).
        "slo": {
            "availability_target": 0.99,
            "latency_p95_ms_target": 500,
            "availability_ok": availability is None or availability >= 0.99,
            "latency_ok": p95 is None or p95 <= 500,
        },
    }
